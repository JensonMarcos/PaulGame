using Unity.Netcode;
using UnityEngine;

public class Objective : NetworkBehaviour
{
    void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        PlayerCharacter playerChar = other.GetComponent<PlayerCharacter>();
        if (playerChar == null) return;
        NetworkObject player = playerChar.transform.root.GetComponent<NetworkObject>();
        int id = PlayerManager.instance.Players.FindIndex(x => x.ClientId == player.OwnerClientId);
        PlayerManager.instance.Players[id].score++;
    }
}
