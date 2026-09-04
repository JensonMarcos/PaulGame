using UnityEngine;
using Unity.Netcode;

// SUPER VIBE CODED BUT WORKS REALLY WELL SOME HOW
public class PredictedProp : NetworkProp
{
    [SerializeField] float velocityBlend = 0.3f;
    [SerializeField] float positionCorrection = 5f;
    [SerializeField] float snapDistance = 2f;
    [SerializeField] float maxLag = 0.25f;

    Collider[] colliders;
    bool wasAsleep;

    bool hasState;
    Vector3 statePosition, stateVelocity, stateAngularVelocity;
    Quaternion stateRotation;

    public override void OnNetworkSpawn()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();

        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (IsServer)
        {
            NetworkManager.NetworkTickSystem.Tick += BroadcastState;
        }
        else
        {
            foreach (Collider remote in Player.RemoteColliders) IgnoreRemoteCollider(remote);
            Player.RemoteColliderAdded += IgnoreRemoteCollider;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) NetworkManager.NetworkTickSystem.Tick -= BroadcastState;
        else Player.RemoteColliderAdded -= IgnoreRemoteCollider;
    }

    public override void ApplyForce(Vector3 force, Vector3 position)
    {
        if (!IsServer) AddImpulse(force, position);
        ApplyForceServerRpc(force, position);
    }

    // Remote players are kinematic capsules teleported by NetworkTransform. Letting them
    // depenetrate the locally simulated prop would fling it; their real effect arrives as state.
    void IgnoreRemoteCollider(Collider remote)
    {
        if (remote == null) return;
        foreach (Collider mine in colliders) Physics.IgnoreCollision(mine, remote, true);
    }

    void BroadcastState()
    {
        bool asleep = rb.IsSleeping();
        if (asleep && wasAsleep) return;
        wasAsleep = asleep;

        StateRpc(rb.position, rb.rotation, rb.linearVelocity, rb.angularVelocity);
    }

    [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
    void StateRpc(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
    {
        statePosition = position;
        stateRotation = rotation;
        stateVelocity = velocity;
        stateAngularVelocity = angularVelocity;
        hasState = true;
    }

    void FixedUpdate()
    {
        if (!hasState) return;
        hasState = false;

        float lag = Mathf.Clamp(NetworkManager.LocalTime.TimeAsFloat - NetworkManager.ServerTime.TimeAsFloat, 0f, maxLag);
        Vector3 error = statePosition + stateVelocity * lag - rb.position;

        if (error.magnitude > snapDistance)
        {
            rb.position = statePosition + stateVelocity * lag;
            rb.rotation = stateRotation;
            rb.linearVelocity = stateVelocity;
            rb.angularVelocity = stateAngularVelocity;
            return;
        }

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, stateVelocity, velocityBlend) + error * positionCorrection;
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, stateAngularVelocity, velocityBlend);
    }
}
