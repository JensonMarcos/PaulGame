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
        int id = PlayerManager.instance.allplayers.FindIndex(x => x.ID == player.OwnerClientId);
        PlayerManager.instance.allplayers[id].score++;
    }
}
