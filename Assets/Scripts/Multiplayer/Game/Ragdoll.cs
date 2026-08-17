using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Ragdoll : NetworkBehaviour
{
    [SerializeField] Rigidbody hips;
    public Transform CameraTarget;

    [Rpc(SendTo.ClientsAndHost)]
    public void ApplyPoseAndVelocityClientRpc(ulong playerNetworkId, Vector3 velocity)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
            return;

        Player player = playerObj.GetComponent<Player>();
        if (player == null) return;

        ApplyPoseAndVelocity(player.playerCharacter.root, velocity);
    }

    void ApplyPoseAndVelocity(Transform sourceRoot, Vector3 velocity)
    {
        var ragBones = GetComponentsInChildren<Transform>(true);
        var rag = new Dictionary<string, Transform>(ragBones.Length);
        foreach (var t in ragBones)
            rag[t.name] = t;

        foreach (var src in sourceRoot.GetComponentsInChildren<Transform>(true))
        {
            if (src.name.Contains("ColliderRotator")) continue;
            if (!rag.TryGetValue(src.name, out var target)) continue;
            target.localRotation = src.localRotation;
        }

        Physics.SyncTransforms();

        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z)) return;

        if(IsServer) hips.linearVelocity = velocity;

        foreach (var body in hips.GetComponentsInChildren<Rigidbody>(true)) {
            if(body.isKinematic) continue;
            body.linearVelocity = velocity;
        }
    }
}
