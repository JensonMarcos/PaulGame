using UnityEngine;
using Unity.Netcode;

public class NetworkProp : NetworkBehaviour
{
    Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) rb.isKinematic = true; // server owns physics
    }

    [ServerRpc(RequireOwnership = false)]
    public void ApplyForceServerRpc(Vector3 force, Vector3 position)
    {
        rb.AddForceAtPosition(force, position, ForceMode.Impulse);
    }
}
