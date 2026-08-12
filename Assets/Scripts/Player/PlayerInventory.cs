using System.Collections;
using Unity.Netcode;
using UnityEngine;

public struct InventoryInputs
{
    public bool Interact;
    public bool Drop;

    public Vector3 Velocity;

    public float Scroll;
    public int NumKeys;
}

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

    [Header("Auto Pickup")]
    [SerializeField] Transform character;
    [SerializeField] float autoPickupRadius = 1f;
    [SerializeField] int autoPickupInterval = 4;
    [SerializeField] float dropPickupCooldown = 1f;
    int autoPickupTick;
    readonly Collider[] autoPickupBuffer = new Collider[8];
    readonly RaycastHit[] pickupHitsBuffer = new RaycastHit[16];
    Item lastDropped;
    float lastDropTime;

    bool wishPickUp;
    bool wishDrop;

    Coroutine readyPullCoroutine;
    
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


    public void SetInputs(InventoryInputs inputs)
    {
        wishPickUp = inputs.Interact;
        wishDrop = inputs.Drop;

        velocity = inputs.Velocity;

        int prevIndex = InvIndex;
        if (inputs.Scroll < 0f)
        { //scrollwheel
            if (InvIndex >= Inventory.Length - 1) InvIndex = 0;
            else InvIndex++;
        }
        if (inputs.Scroll > 0f)
        {
            if (InvIndex <= 0) InvIndex = Inventory.Length - 1;
            else InvIndex--;
        }
        if (inputs.NumKeys >= 0 && inputs.NumKeys < Inventory.Length)
        {
            InvIndex = inputs.NumKeys;
        }

        if(prevIndex != InvIndex && Inventory[prevIndex] != Inventory[InvIndex])
        {
            ReadyPull = false;

            float _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;

            if(readyPullCoroutine != null) StopCoroutine(readyPullCoroutine);
            readyPullCoroutine = StartCoroutine(WaitToReadyPull(_pullOutTime));

            Select(InvIndex, _pullOutTime, true);
            SelectServerRpc(InvIndex, _pullOutTime, true);
        } 


        if(wishDrop && Inventory[InvIndex] != handsItem)
        {
            Drop(InvIndex);
        }
    }

    public void TryPickUp() {
        if(++autoPickupTick >= autoPickupInterval)
        {
            autoPickupTick = 0;
            AutoPickUp();
        }

        //find target (change later to be better)
        int hitCount = Physics.RaycastNonAlloc(cam.position, cam.forward, pickupHitsBuffer, pickupDistance, pickupMask, QueryTriggerInteraction.Collide);

        Item closestInner = null;
        float innerDistance = Mathf.Infinity;

        Item closestOuter = null;
        float outerDistance = Mathf.Infinity;

        for(int h = 0; h < hitCount; h++)
        {
            RaycastHit hit = pickupHitsBuffer[h];
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

        //pickup/hover logic
        Item selectedItem = closestInner != null ? closestInner : closestOuter;

        if(selectedItem != currentHovered)
        {
            if(currentHovered != null) currentHovered.SetHovered(false);

            currentHovered = selectedItem;

            if(currentHovered != null) currentHovered.SetHovered(true);
        }

        if(selectedItem == null) return;

        if(!wishPickUp) return;

        PickupItem(selectedItem);
    }

    void AutoPickUp()
    {
        int count = Physics.OverlapSphereNonAlloc(character.position, autoPickupRadius, autoPickupBuffer, itemLayer, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Item item = autoPickupBuffer[i].transform.root.GetComponent<Item>();
            if(item == null) continue;

            if(item == lastDropped && Time.time - lastDropTime < dropPickupCooldown) continue; //just dropped

            int slot = item.data.slot;
            if(Inventory[slot] != handsItem) continue; //slot already filled

            PickupItem(item);
        }
    }

    void PickupItem(Item item)
    {
        if(item.HasOwner.Value) return;

        int slot = item.data.slot;

        if(Inventory[slot] != handsItem) Drop(slot);

        item.ItemPickupServerRpc(transform.root.GetComponent<NetworkObject>());

        Inventory[slot] = item.gameObject;
        NetworkIDInventory[slot] = item.GetComponent<NetworkObject>().NetworkObjectId;

        float _pullOutTime;
        bool _animate = false;
        if(Inventory[InvIndex] == handsItem || slot == InvIndex)
        {
            InvIndex = slot;
            ReadyPull = false;
            _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;
            if(readyPullCoroutine != null) StopCoroutine(readyPullCoroutine);
            readyPullCoroutine = StartCoroutine(WaitToReadyPull(_pullOutTime));
            _animate = true;
        }

        SyncClientInventory();

        _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;
        Select(InvIndex, _pullOutTime, _animate);
        SelectServerRpc(InvIndex, _pullOutTime, _animate);
    }

    public void RevertPickup(Item item)
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if(Inventory[i] != item.gameObject) continue;

            Inventory[i] = handsItem;
            NetworkIDInventory[i] = 0UL;
            SyncClientInventory();

            float _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;
            Select(InvIndex, _pullOutTime, true);
            SelectServerRpc(InvIndex, _pullOutTime, true);
            return;
        }
    }

    void Drop(int i)
    {
        lastDropped = Inventory[i].GetComponent<Item>();
        lastDropTime = Time.time;

        Inventory[i].GetComponent<Item>().ItemDropServerRpc(transform.position, transform.rotation, velocity + cam.forward * throwForce, ClientInventory[i].Ammo);
        Inventory[i] = handsItem;
        NetworkIDInventory[i] = 0UL;
        SyncClientInventory();

        float _pullOutTime = ClientInventory[InvIndex].data.pullOutTime;
        Select(i, _pullOutTime, true);
        SelectServerRpc(i, _pullOutTime, true);
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

    public void GiveItem(ulong itemNetworkId)
    {
        NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out var netObj);
        if(netObj == null) return;
        PickupItem(netObj.GetComponent<Item>());
    }

    public void ClearItem(int itemId = -1) //-1 or no param to clear full inventory
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if(Inventory[i] == handsItem) continue;
            if(itemId != -1 && GameManager.instance.itemList.GetItemId(Inventory[i]) != itemId) continue;
            Inventory[i] = handsItem;
            NetworkIDInventory[i] = 0UL;
        }
        SyncClientInventory();
    }

    [Rpc(SendTo.Server)]
    public void SelectServerRpc(int _index, float _pullOutTime, bool animate, RpcParams rpcParams = default)
    {
        SelectClientRpc(_index, _pullOutTime, animate, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void SelectClientRpc(int _index, float _pullOutTime, bool animate, RpcParams rpcParams = default)
    {
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
 
    public void Deselect() {
        if(currentHovered != null) currentHovered.SetHovered(false);
        currentHovered = null;
    }
}
