using Unity.Netcode;
using UnityEngine;

public class Killzone : NetworkBehaviour
{
    public enum Mode { KillAndTeleport, Kill, Teleport }
    public Mode mode = Mode.KillAndTeleport;

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        PlayerCharacter playerChar = other.GetComponent<PlayerCharacter>();

        if (playerChar == null) return;
        NetworkObject player = playerChar.transform.root.GetComponent<NetworkObject>();

        switch (mode)
        {
            case Mode.KillAndTeleport:
                PlayerManager.instance.DealDamageServerRpc(player.OwnerClientId, 1234f, Vector3.zero);
                PlayerManager.instance.TeleportServerRpc(player.OwnerClientId, GameManager.instance.rooms.current.objectivePoint.position);
                break;
            case Mode.Kill:
                PlayerManager.instance.DealDamageServerRpc(player.OwnerClientId, 1234f, Vector3.zero);
                break;
            case Mode.Teleport:
                PlayerManager.instance.TeleportServerRpc(player.OwnerClientId, GameManager.instance.rooms.current.objectivePoint.position);
                break;
        }
    }
}
