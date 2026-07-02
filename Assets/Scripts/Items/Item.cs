using Unity.Netcode;
using UnityEngine;

public class Item : NetworkBehaviour
{
    public NetworkVariable<bool> HasOwner = new(false);
    public NetworkVariable<int> Ammo = new();

    public GameObject clientPrefab;
    public ItemData data;

    public bool hovered;
    [SerializeField] GameObject model;
    Collider itemCollider;
    Rigidbody rb;
    Outline outline;

    public override void OnNetworkSpawn()
    {
        itemCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        outline = GetComponent<Outline>();
        outline.OutlineWidth = 5f;
        outline.enabled = false;

        if(!IsOwner) rb.isKinematic = true;
        
        if(IsServer)
        {
            if(PlayerManager.instance != null)
            {
                Ammo.Value = PlayerManager.instance.reloadEnabled ? data.ammoCap : data.ammoSpawn;
            } else
            {
                Ammo.Value = data.ammoCap;
            }
            
        } 
    }

    public void SetHovered(bool value)
    {
        if (hovered == value) return;

        hovered = value;

        outline.enabled = hovered;
    }

    [Rpc(SendTo.Server)]
    public void ItemPickupServerRpc(NetworkObjectReference netObj)
    {
        if(HasOwner.Value) return;
        HasOwner.Value = true;
        //OwnerNetObj.Value = netObj;
        rb.isKinematic = true;
        transform.position = Vector3.down;

        model.SetActive(false);
        itemCollider.enabled = false;
    }

    [Rpc(SendTo.Server)]
    public void ItemDropServerRpc(Vector3 pos, Quaternion rot, Vector3 vel, int ammo)
    {
        if(!HasOwner.Value) return;

        transform.position = pos; //reduntant but idk it was bugging without
        transform.rotation = rot;

        rb.isKinematic = false;
        rb.position = pos; 
        rb.rotation = rot;
        rb.linearVelocity = vel;

        HasOwner.Value = false;

        Ammo.Value = ammo;

        model.SetActive(true);
        itemCollider.enabled = true;
    }


    [Rpc(SendTo.Server)]
    public void ItemClearServerRpc()
    {
        GameManager.instance.worldObjects.Remove(gameObject);
        GetComponent<NetworkObject>().Despawn(true);
    }
}
