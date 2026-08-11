using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum ObjectiveType
{
    Continuous,
    Sequential
}

public class Objective : NetworkBehaviour
{
    [SerializeField] ObjectiveType objectiveType = ObjectiveType.Continuous;

    [Header("Continuous")]
    [SerializeField] int scorePerTick = 1;

    [Header("Sequential")]
    [SerializeField] int firstEntryScore = 100;
    [SerializeField] int entryScoreStep = 25;
    [SerializeField] int minEntryScore = 10;

    readonly HashSet<ulong> claimedClients = new HashSet<ulong>();

    void OnTriggerStay(Collider other)
    {
        if (!IsServer) return;
        if (objectiveType != ObjectiveType.Continuous) return;
        if (!IsPlayer(other)) return;

        AddScore(other, scorePerTick);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (objectiveType != ObjectiveType.Sequential) return;
        if (!IsPlayer(other)) return;

        PlayerCharacter playerChar = other.GetComponent<PlayerCharacter>();
        if (playerChar == null) return;

        NetworkObject player = playerChar.transform.root.GetComponent<NetworkObject>();
        if (player == null) return;
        if (!IsInCurrentRoom(player.gameObject)) return;

        //reward each client only once
        if (!claimedClients.Add(player.OwnerClientId)) return;

        int reward = Mathf.Max(minEntryScore, firstEntryScore - entryScoreStep * (claimedClients.Count - 1));
        AddScore(player.OwnerClientId, reward);
    }

    static bool IsPlayer(Collider other)
    {
        return other.gameObject.layer == LayerMask.NameToLayer("Player");
    }

    static bool IsInCurrentRoom(GameObject playerRoot)
    {
        Room current = GameManager.instance?.rooms.current;
        return current != null && current.playersInRoom.Contains(playerRoot);
    }

    void AddScore(Collider other, int amount)
    {
        PlayerCharacter playerChar = other.GetComponent<PlayerCharacter>();
        if (playerChar == null) return;

        NetworkObject player = playerChar.transform.root.GetComponent<NetworkObject>();
        if (player == null) return;
        if (!IsInCurrentRoom(player.gameObject)) return;

        AddScore(player.OwnerClientId, amount);
    }

    void AddScore(ulong clientId, int amount)
    {
        int id = PlayerManager.instance.Players.FindIndex(x => x.ClientId == clientId);
        if (id < 0) return;

        PlayerManager.instance.Players[id].score += amount;
    }
}
