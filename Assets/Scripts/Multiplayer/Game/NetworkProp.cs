using UnityEngine;
using Unity.Netcode;

public class NetworkProp : NetworkBehaviour
{
    public Rigidbody rb;
    
    public override void OnNetworkSpawn()
    {
        if(rb == null) rb = GetComponent<Rigidbody>();

        if (!IsServer) rb.isKinematic = true;
    }

    [Rpc(SendTo.Server)]
    public void ApplyForceServerRpc(Vector3 force, Vector3 position)
    {
        if (float.IsNaN(force.x) || float.IsNaN(force.y) || float.IsNaN(force.z)) return;
        rb.AddForceAtPosition(force, position, ForceMode.Impulse);
    }
}
