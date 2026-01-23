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
    [SerializeField] Transform cam;
    [SerializeField] float pickupDistance;
    //[SerializeField] float grabWidth;
    [SerializeField] LayerMask pickupMask;
    [SerializeField] LayerMask itemLayer;
    [SerializeField] LayerMask pickupColliderLayer;
    [SerializeField] LayerMask worldMask;
    [SerializeField] float throwForce;
    Item currentHovered;

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

            Select(InvIndex, _pullOutTime, true);
            SelectServerRpc(InvIndex, _pullOutTime, true);
        } 


        if(wishDrop && Inventory[InvIndex] != handsItem)
        {
            Drop(InvIndex);
        }
    }

    public void TryPickUp() {
        Item selectedItem = GetTarget();

        if(selectedItem != currentHovered)
        {
            if(currentHovered != null) currentHovered.SetHovered(false);

            currentHovered = selectedItem;

            if(currentHovered != null) currentHovered.SetHovered(true);
        }

        if(selectedItem == null) return;

        if(!wishPickUp) return;

        int slot = selectedItem.data.slot;

        if(Inventory[slot] != handsItem) Drop(slot);

        selectedItem.GetComponent<Item>().ItemPickupServerRpc(transform.root.GetComponent<NetworkObject>());

        Inventory[slot] = selectedItem.gameObject;
        NetworkIDInventory[slot] = selectedItem.GetComponent<NetworkObject>().NetworkObjectId;

        float _pullOutTime;
        bool _animate = false;
        if(Inventory[InvIndex] == handsItem || slot == InvIndex) { //equip item if hands out or if pickup in selected index
            InvIndex = slot;

            ReadyPull = false;
            
            _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;
            StopCoroutine("WaitToReadyPull");
            StartCoroutine(WaitToReadyPull(_pullOutTime));

            _animate = true;
        }

        SyncClientInventory();

        _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;
        Select(InvIndex, _pullOutTime, _animate);
        SelectServerRpc(InvIndex, _pullOutTime, _animate);

    }

    Item GetTarget()
    {
        RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, pickupDistance, pickupMask, QueryTriggerInteraction.Collide);

        Item closestInner = null;
        float innerDistance = Mathf.Infinity;

        Item closestOuter = null;
        float outerDistance = Mathf.Infinity;

        foreach(RaycastHit hit in hits)
        {
            Item _item = hit.transform.root.GetComponent<Item>();
            
            if(_item == null) continue;

            int layer = hit.collider.gameObject.layer;

            if((1 << layer & itemLayer) != 0) //item layer
            {
                if(hit.distance < innerDistance)
                {
                    innerDistance = hit.distance;
                    closestInner = _item;
                }
            } else if((1 << layer & pickupColliderLayer) != 0) //pickup collider layer
            {
                //LOS check
                Vector3 dir = _item.transform.position - cam.transform.position;
                if(Physics.Raycast(cam.position, dir.normalized, dir.magnitude, worldMask)) continue;

                if(hit.distance < outerDistance)
                {
                    outerDistance = hit.distance;
                    closestOuter = _item;
                }
            }
        }

        return closestInner != null ? closestInner : closestOuter;
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
    public void SelectServerRpc(int _index, float _pullOutTime, bool animate)
    {
        SelectClientRpc(_index, _pullOutTime, animate);
    }

    [ClientRpc]
    public void SelectClientRpc(int _index, float _pullOutTime, bool animate)
    {
        if(IsOwner) return;
        Select(_index, _pullOutTime, animate);
    }    
    
    public void Select(int _index, float _pullOutTime, bool animate)
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i] != handsItem)
            {
                ClientInventory[i].gameObject.SetActive(i == _index);
            }
        }
        
        if(animate) animations.SwitchItemAnimation(_pullOutTime*2f);
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
