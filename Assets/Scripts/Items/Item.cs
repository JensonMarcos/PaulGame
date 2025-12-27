using Unity.Netcode;
using UnityEngine;

public class Item : NetworkBehaviour
{
    public NetworkVariable<bool> HasOwner = new(false);
    public NetworkVariable<NetworkObjectReference> OwnerNetObj = new();

    public GameObject clientPrefab;
    public ItemData data;

    public bool hovered;
    [SerializeField] GameObject model;
    Collider itemCollider;
    Rigidbody rb;
    Outline outline;

    Transform ownerTransform;

    public override void OnNetworkSpawn()
    {
        HasOwner.OnValueChanged += OnOwnerChanged;
    }

    public override void OnNetworkDespawn()
    {
        HasOwner.OnValueChanged -= OnOwnerChanged;
    }

    void Start()
    {
        itemCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        outline = GetComponent<Outline>();
        outline.OutlineWidth = 5f;
        outline.enabled = false;

        if(!IsOwner) rb.isKinematic = true;
    }

    void LateUpdate()
    {
        if(HasOwner.Value)
        {
            if(ownerTransform == null)
            {
                if(OwnerNetObj.Value.TryGet(out var obj))
                {
                    ownerTransform = obj.gameObject.GetComponent<Player>().playerInventory.transform;
                }
            } else
            { //called next frame, prob not an issue
                transform.position = ownerTransform.position;
                transform.rotation = ownerTransform.rotation;                
            }
        } else
        {
            outline.enabled = hovered;
            if(hovered) hovered = false;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ItemPickupServerRpc(NetworkObjectReference netObj)
    {
        if(HasOwner.Value) return;
        HasOwner.Value = true;
        OwnerNetObj.Value = netObj;
        rb.isKinematic = true;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ItemDropServerRpc(Vector3 pos, Quaternion rot, Vector3 vel)
    {
        if(!HasOwner.Value) return;

        
        rb.isKinematic = false;
        rb.position = pos;
        rb.rotation = rot;
        rb.linearVelocity = vel;

        HasOwner.Value = false;
    }

    void OnOwnerChanged(bool previousValue, bool newValue)
    {
        if(newValue) //just assigned to owner
        {
            model.SetActive(false);
            itemCollider.enabled = false;

            if(OwnerNetObj.Value.TryGet(out var obj))
            {
                ownerTransform = obj.gameObject.GetComponent<Player>().playerInventory.transform;
            }
        } else //unassigned
        {
            model.SetActive(true);
            itemCollider.enabled = true;
            
            ownerTransform = null;
        }
    }
}
