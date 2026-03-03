using Unity.Netcode;
using UnityEngine;

public class Killzone : NetworkBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        PlayerCharacter playerChar = other.GetComponent<PlayerCharacter>();

        if (playerChar == null) return;
        NetworkObject player = playerChar.transform.root.GetComponent<NetworkObject>();
  
        PlayerManager.instance.DealDamageServerRpc(player.OwnerClientId, 1234f, Vector3.zero);
    }
}
