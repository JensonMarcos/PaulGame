using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Item : NetworkBehaviour
{
    public ItemData data;
    public Transform ownerTrans, gunTrans;
    public Rigidbody rb;
    public GameObject ghostItemPrefab;
    public PlayerManager pmanager;
    //public GameObject activePlayerItem;

    public Transform localGunTrans;
    public LayerMask thrownLM;

    public NetworkVariable<int> serverAmmo = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int ammo;
    public GameObject model;
    public Outline outline;
    public bool hovered = false;

    public Transform rhand, lhand;

    void Start() {
        rb = GetComponent<Rigidbody>();
        pmanager = PlayerManager.instance;
        outline = GetComponent<Outline>();
        outline.OutlineWidth = 5f;
        outline.enabled = false;
        if(model == null) model = transform.Find("Model").gameObject;
        if(IsServer) {
            serverAmmo.Value = data.ammoCap;
        }
    }

    void Update() {
        //if(transform.root != transform) GetComponent<MeshRenderer>().enabled = GetComponent<BoxCollider>().enabled;

        // if(IsServer && transform.root != transform) {
        //     rb.Move(gunTrans.position, gunTrans.rotation);
        //     // transform.localPosition = transform.root.InverseTransformPoint(gunTrans.position);
        //     // transform.localRotation = Quaternion.Inverse(transform.root.rotation) * gunTrans.rotation;
        // } 
    }

    void LateUpdate()
    {
        outline.enabled = hovered;
        if(hovered) hovered = false;

        // if(transform.root != transform && transform.position != transform.root.GetComponent<Inventory>().gunHolder.position) {
        //     transform.position = transform.root.GetComponent<Inventory>().gunHolder.position;
        //     transform.rotation = transform.root.GetComponent<Inventory>().gunHolder.rotation;
        // }
    }

    void FixedUpdate()
    {
        if(IsServer && transform.root != transform) {
            rb.Move(gunTrans.position, gunTrans.rotation);
            // transform.localPosition = transform.root.InverseTransformPoint(gunTrans.position);
            // transform.localRotation = Quaternion.Inverse(transform.root.rotation) * gunTrans.rotation;
        } 
    }

    [ServerRpc(RequireOwnership = false)]
    public void ItemPickupServerRpc(bool dropped, Vector3 throwForce, Vector3 velocity, ServerRpcParams serverRpcParams = default) {
        pmanager = PlayerManager.instance;
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        for(int i = 0; i < PlayerManager.instance.allplayers.Count; i++) {
            if(PlayerManager.instance.allplayers[i].ID == clientId) {
                ownerTrans = PlayerManager.instance.allplayers[i].playerGameObject.transform;
                gunTrans = ownerTrans.GetComponent<Inventory>().gunHolderThirdPerson;
            }
        }

        // ClientRpcParams clientRpcParams = new ClientRpcParams {
        //     Send = new ClientRpcSendParams
        //     {
        //         TargetClientIds = new ulong[]{clientId}
        //     }
        // };

        if(dropped) {
            GetComponent<Collider>().enabled = true;
            rb.excludeLayers = thrownLM;
            StartCoroutine(DelayCollider());
            //rb.useGravity = true;
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
            rb.AddForce(throwForce, ForceMode.Impulse);
            gameObject.layer = LayerMask.NameToLayer("Item");
            //GetComponent<MeshRenderer>().enabled = true;
            ItemPickupClientRpc(true);

            // activePlayerItem.GetComponent<NetworkObject>().DontDestroyWithOwner = true;
            // activePlayerItem.GetComponent<NetworkObject>().Despawn();

            //ownerTrans.GetComponent<Inventory>().UpdateInventoryClientRpc(clientRpcParams);      
            ownerTrans = null;
            gunTrans = null;

            //GetComponent<NetworkRigidbody>().UseRigidBodyForMotion = true;
            GetComponent<NetworkObject>().TryRemoveParent(true);
            //GetComponent<NetworkObject>().ChangeOwnership(NetworkManager.ServerClientId);
            
            transform.parent = null;

            return;
        }

        GetComponent<Collider>().enabled = false;
        //rb.useGravity = false;
        rb.isKinematic = true;
        gameObject.layer = LayerMask.NameToLayer("Player");
        //GetComponent<MeshRenderer>().enabled = false;

        //GetComponent<NetworkObject>().ChangeOwnership(clientId);
        //GetComponent<NetworkRigidbody>().UseRigidBodyForMotion = false;
        GetComponent<NetworkObject>().TrySetParent(ownerTrans, true);
        
        transform.parent = ownerTrans;
        transform.position = gunTrans.position;
        transform.rotation = gunTrans.rotation;

        ItemPickupClientRpc(false);
        //ownerTrans.root.GetComponent<Inventory>().UpdateInventoryClientRpc(clientRpcParams);
        

        // GameObject playerItem = Instantiate(playerItemPrefab, ownerTrans); 
        // playerItem.GetComponent<NetworkObject>().SpawnWithOwnership(serverRpcParams.Receive.SenderClientId);
        // playerItem.GetComponent<NetworkObject>().TrySetParent(ownerTrans, true);
        // playerItem.transform.parent = ownerTrans;
        // playerItem.transform.position = ownerTrans.transform.position;
        // playerItem.transform.rotation = ownerTrans.transform.rotation;

        //activePlayerItem = playerItem;
        

    }

    [ClientRpc]
    public void ItemPickupClientRpc(bool dropped) {
        if(dropped) {
            GetComponent<Collider>().enabled = true;
            gameObject.layer = LayerMask.NameToLayer("Item");
            // GetComponent<NetworkRigidbody>().UseRigidBodyForMotion = true;
            //GetComponent<NetworkTransform>().InLocalSpace = false;
            //GetComponent<MeshRenderer>().enabled = true;
        } else { //picked up
            GetComponent<Collider>().enabled = false;
            gameObject.layer = LayerMask.NameToLayer("Player");
            // GetComponent<NetworkRigidbody>().UseRigidBodyForMotion = false;
            //GetComponent<NetworkTransform>().InLocalSpace = true;
            model.SetActive(false);
            ammo = serverAmmo.Value;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SelectServerRpc(bool selected) {
        //if(GetComponent<MeshRenderer>().enabled != selected) GetComponent<MeshRenderer>().enabled = selected;
        SelectClientRpc(selected);
    }

    [ClientRpc]
    public void SelectClientRpc(bool selected) {
        if(transform.root.GetComponent<NetworkObject>().IsOwner) {
            model.SetActive(false);
            return;
        }
        if(model.activeSelf != selected) model.SetActive(selected);
    }

    IEnumerator DelayCollider() {
        yield return new WaitForSeconds(1);
        rb.excludeLayers = LayerMask.NameToLayer("Ghost");
    }

    [ServerRpc(RequireOwnership = false)]
    public void UpdateAmmoServerRpc(int newAmmo, ServerRpcParams serverRpcParams = default) {
        serverAmmo.Value = newAmmo;
    }
}