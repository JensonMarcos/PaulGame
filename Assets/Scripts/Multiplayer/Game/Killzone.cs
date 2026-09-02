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
                PlayerManager.instance.WorldDamage(player.OwnerClientId, 1000f, Vector3.zero);
                GameManager.instance.GameTeleport(player.OwnerClientId);
                break;
            case Mode.Kill:
                PlayerManager.instance.WorldDamage(player.OwnerClientId, 1000f, Vector3.zero);
                break;
            case Mode.Teleport:
                GameManager.instance.GameTeleport(player.OwnerClientId);
                break;
        }
    }
}
