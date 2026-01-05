using Unity.Netcode;
using UnityEngine;

public class Objective : NetworkBehaviour
{
    void Start()
    {
        if (!IsServer) Destroy(this);
    }

    void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        NetworkObject player = other.GetComponent<NetworkObject>();
        if (player == null) return;
        int id = PlayerManager.instance.Players.FindIndex(x => x.ClientId == player.OwnerClientId);
        PlayerManager.instance.Players[id].score++;
    }
}
