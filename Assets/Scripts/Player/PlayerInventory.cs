using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public NetworkList<ulong> NetworkIDInventory = new NetworkList<ulong>(
        writePerm: NetworkVariableWritePermission.Owner,
        readPerm: NetworkVariableReadPermission.Everyone
    );

    public GameObject[] Inventory = new GameObject[3];
    public ItemClient[] ClientInventory = new ItemClient[3];

    public int InvIndex;
    public bool ReadyPull;
    public bool Reloading;

    [SerializeField] GameObject handsItem;

    [SerializeField] PlayerAnimations animations;

    [Header("Item Pickup")]
    [SerializeField] float grabDistance;
    [SerializeField] float grabWidth;
    [SerializeField] LayerMask itemMask;
    [SerializeField] Transform cam;
    [SerializeField] float throwForce;

    bool wishPickUp;
    bool wishDrop;
    
    Vector3 velocity;

    public override void OnNetworkSpawn()
    {
        NetworkIDInventory.OnListChanged += OnIDListChanged;
    }

    public override void OnNetworkDespawn()
    {
        NetworkIDInventory.OnListChanged -= OnIDListChanged;
    }

    public void Initialize()
    {
        if (IsOwner) {
            for (int i = 0; i < Inventory.Length; i++)
            {
                NetworkIDInventory.Add(0UL); //zero representing hand item
            }
        }

        SyncClientInventory();
    }


    public void SetInputs(PlayerInputs _inputs, Vector3 _velocity)
    {
        wishPickUp = _inputs.Interact;
        wishDrop = _inputs.Drop;

        velocity = _velocity;

        int prevIndex = InvIndex;
        if (_inputs.ScrollWheel < 0f)
        { //scrollwheel
            if (InvIndex >= Inventory.Length - 1) InvIndex = 0;
            else InvIndex++;
        }
        if (_inputs.ScrollWheel > 0f)
        {
            if (InvIndex <= 0) InvIndex = Inventory.Length - 1;
            else InvIndex--;
        }
        if (_inputs.NumKey >= 0 && _inputs.NumKey < Inventory.Length)
        {
            InvIndex = _inputs.NumKey;
        }

        if(prevIndex != InvIndex && Inventory[prevIndex] != Inventory[InvIndex])
        {
            ReadyPull = false;

            float _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;

            StopCoroutine("WaitToReadyPull");
            StartCoroutine(WaitToReadyPull(_pullOutTime));

            Select(InvIndex, _pullOutTime);
            SelectServerRpc(InvIndex, _pullOutTime);
        } 


        if(wishDrop && Inventory[InvIndex] != handsItem)
        {
            Drop(InvIndex);
        }
    }

    public void TryPickUp() {
        if(Physics.SphereCast(cam.position, grabWidth, cam.forward, out RaycastHit hit, grabDistance, itemMask.value)) {
            if(Physics.Raycast(cam.position, cam.forward, out RaycastHit hit2, grabDistance, itemMask.value)) {
                hit = hit2; //when 2 items, chose one at crosshair rather than closest, change later
            }

            if(!wishPickUp) { //hovering code, kinda inefficient
                hit.transform.GetComponent<Item>().hovered = true;
                return;
            } 

            GameObject selectedItem = hit.transform.gameObject;
            int slot = selectedItem.GetComponent<Item>().data.slot;

            if(Inventory[slot] != handsItem) Drop(slot);

            selectedItem.GetComponent<Item>().ItemPickupServerRpc(transform.root.GetComponent<NetworkObject>());

            Inventory[slot] = selectedItem;
            NetworkIDInventory[slot] = selectedItem.GetComponent<NetworkObject>().NetworkObjectId;

            if(Inventory[InvIndex] == handsItem || slot == InvIndex) { //equip item if hands out or if pickup in selected index
                InvIndex = slot;

                float _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;

                StopCoroutine("WaitToReadyPull");
                StartCoroutine(WaitToReadyPull(_pullOutTime));

                Select(InvIndex, _pullOutTime);
                SelectServerRpc(InvIndex, _pullOutTime);
            }

            SyncClientInventory();
        }
    }

    void Drop(int i)
    {
        Inventory[i].GetComponent<Item>().ItemDropServerRpc(transform.position, transform.rotation, velocity + cam.forward * throwForce, ClientInventory[i].Ammo);
        Inventory[i] = handsItem;
        NetworkIDInventory[i] = 0UL;
        SyncClientInventory();
    }

    public void DropAll()
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if(Inventory[i] != handsItem)
            {
                Drop(i);
            }
        }
    }

    [ServerRpc(RequireOwnership = true)]
    public void SelectServerRpc(int _index, float _pullOutTime)
    {
        SelectClientRpc(_index, _pullOutTime);
    }

    [ClientRpc]
    public void SelectClientRpc(int _index, float _pullOutTime)
    {
        if(IsOwner) return;
        Select(_index, _pullOutTime);
    }    
    
    public void Select(int _index, float _pullOutTime)
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i] != handsItem)
            {
                ClientInventory[i].gameObject.SetActive(i == _index);
            }
        }
        
        animations.SwitchItemAnimation(_pullOutTime*1.5f);
    }

    IEnumerator WaitToReadyPull(float _pullOutTime) {
        yield return new WaitForSeconds(_pullOutTime);
        ReadyPull = true;
    }

    void OnIDListChanged(NetworkListEvent<ulong> changeEvent)
    {
        if(IsOwner) return;

        int i = changeEvent.Index;

        if(changeEvent.Type != NetworkListEvent<ulong>.EventType.Value) return;

        if(changeEvent.Value == 0UL && Inventory[i] != handsItem) //change to hands
        {
            Inventory[i] = handsItem;

        } else //change to item
        {
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(changeEvent.Value, out var _netObj);
            if(_netObj != null && Inventory[i] != _netObj.gameObject) Inventory[i] = _netObj.gameObject;
        }

        SyncClientInventory();
    }

    void SyncClientInventory()
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if(ClientInventory[i] == null) ClientInventory[i] = handsItem.GetComponent<ItemClient>();


            if(Inventory[i] != handsItem)
            {
                if(ClientInventory[i].data == Inventory[i].GetComponent<Item>().data) continue;

                if(ClientInventory[i].gameObject != handsItem) Destroy(ClientInventory[i].gameObject);

                GameObject clientItem = Instantiate(Inventory[i].GetComponent<Item>().clientPrefab, transform);
                clientItem.transform.localPosition = Vector3.zero;
                clientItem.transform.localEulerAngles = Vector3.zero;
                ClientInventory[i] = clientItem.GetComponent<ItemClient>();

                ClientInventory[i].Ammo = Inventory[i].GetComponent<Item>().Ammo.Value;
            } else
            {
                if(ClientInventory[i].gameObject == handsItem) continue;

                Destroy(ClientInventory[i].gameObject);

                ClientInventory[i] = handsItem.GetComponent<ItemClient>();
            }
            
        }
    }
 

}
