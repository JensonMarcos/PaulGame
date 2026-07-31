using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Ragdoll : NetworkBehaviour
{
    [SerializeField] Rigidbody hips;

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

        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
            body.linearVelocity = velocity;
    }
}
