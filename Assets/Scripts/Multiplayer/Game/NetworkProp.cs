using UnityEngine;
using Unity.Netcode;

public class NetworkProp : NetworkBehaviour
{
    public Rigidbody rb;

    [SerializeField] bool doClientPhysics = false;

    [SerializeField] float correctionThreshold = 0.25f;
    [SerializeField] float correctionSpeed = 8f;
    [SerializeField] float snapDistance = 2f;

    NetworkVariable<Vector3> netPosition = new();
    NetworkVariable<Quaternion> netRotation = new();
    NetworkVariable<Vector3> netVelocity = new();

     
    
    public override void OnNetworkSpawn()
    {
        if(rb == null) rb = GetComponent<Rigidbody>();

        if (!IsServer && !doClientPhysics) rb.isKinematic = true;

        if(!IsServer && doClientPhysics) rb.useGravity = false;

    }

    void FixedUpdate()
    {
        if(!IsSpawned) return;
        if(!doClientPhysics) return;

        if(IsServer)
        {
            netPosition.Value = rb.position;
            netVelocity.Value = rb.linearVelocity;
            netRotation.Value = rb.rotation;
        } 
        else
        {
            Vector3 posError = netPosition.Value - rb.position;

            if (posError.magnitude > snapDistance)
            {
                rb.position = netPosition.Value;
                rb.rotation = netRotation.Value;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            

            rb.linearVelocity = netVelocity.Value;
            rb.rotation = netRotation.Value;

            if(posError.magnitude < correctionThreshold && netVelocity.Value.magnitude > 0.1f) return;

            // Smooth positional correction
            rb.position = Vector3.Lerp(
                rb.position,
                netPosition.Value,
                Time.fixedDeltaTime * correctionSpeed);
        }
    }

    [Rpc(SendTo.Server)]
    public void ApplyForceServerRpc(Vector3 force, Vector3 position)
    {
        rb.AddForceAtPosition(force, position, ForceMode.Impulse);
    }
}
