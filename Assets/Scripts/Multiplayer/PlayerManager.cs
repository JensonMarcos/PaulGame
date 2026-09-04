using System;
using System.Collections.Generic;
using System.Linq;
using Netcode.Transports.Facepunch;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager instance;
    public GameObject playerPrefab, ragdollPrefab;
    public List<PlayerData> Players = new List<PlayerData>();
    public int playersAlive = 0;

    public NetworkVariable<bool> damageEnabled = new();
    public NetworkVariable<bool> reloadEnabled = new(true);

    public override void OnNetworkSpawn() {
        // if(instance) {
        //     Destroy(gameObject);
        //     return;
        // }
        instance = this;
        
        if(!IsServer) return;
        
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedCallback;

        List<ulong>clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        print(clients.Count);
        foreach(ulong id in clients) {
            SpawnPlayer(id);
        }

        damageEnabled.Value = true;
    }

    public override void OnNetworkDespawn() {
        if(!IsServer) return;
        NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedCallback;
    }

    void OnClientConnectedCallback(ulong id)
    {
        if(!IsServer) return;
        SpawnPlayer(id);
    }

    void OnClientDisconnectedCallback(ulong id)
    {
        if(!IsServer) return;

        int index = Players.FindIndex(x => x.ClientId == id);
        if(index < 0) return;

        PlayerData disconnectedPlayer = Players[index];
        ClearItem(id);

        if(GameManager.instance != null) {
            GameManager.instance.rooms.previous?.RemoveFromRoom(disconnectedPlayer.playerGameObject);
            GameManager.instance.rooms.current?.RemoveFromRoom(disconnectedPlayer.playerGameObject);
        }

        Players.RemoveAt(index);
        playersAlive = Players.Count(x => !x.isDead);

        foreach(PlayerData player in Players)
        {
            if(player.lastAttackedBy == id) player.lastAttackedBy = null;
            if(player.player != null && player.player.NetworkObject.IsSpawned)
                player.player.AddOrRemoveScoreboardItemClientRpc(false, id, "", 0, 0, 0);
        }
    }

    void SpawnPlayer(ulong id) {
        foreach(PlayerData _player in Players) {
            if(_player.ClientId == id) return;
        }

        Vector3 spawnPos = Vector3.right * (Players.Count * 1.5f); //kinda dumb but works good enough
        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
        PlayerData newPlayer = new PlayerData(player, id, 100f, GetPlayerName(id));
        Players.Add(newPlayer);

        playersAlive = Players.Count(x => x.isDead == false);

        //add existing players to scoreboard of new player
        foreach(PlayerData _player in Players) {
            if(_player.ClientId == id) continue;
            newPlayer.player.AddOrRemoveScoreboardItemClientRpc(true, _player.ClientId, _player.name, _player.wins, _player.kills, _player.deaths);
        }

        //add new player to scoreboards of all players (including self)
        foreach(PlayerData _player in Players) {
            _player.player.AddOrRemoveScoreboardItemClientRpc(true, id, newPlayer.name, 0, 0, 0);
        }
    }

    string GetPlayerName(ulong id)
    {
        if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is FacepunchTransport)
        {
            if (id == NetworkManager.ServerClientId) return SteamClient.Name;

            if (SteamManager.Instance != null && SteamManager.Instance.Players != null)
            {
                foreach (Friend friend in SteamManager.Instance.Players)
                {
                    if (friend.Id == SteamClient.SteamId) continue;
                    if (Players.All(p => p.name != friend.Name)) return friend.Name;
                }
            }
        }
        return "Player" + id;
    }

    public void AssignTeamsRandomly(int numberOfTeams)
    {
        List<PlayerData> shuffled = new List<PlayerData>(Players);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        for (int i = 0; i < shuffled.Count; i++)
            shuffled[i].team = i % numberOfTeams;
    }

    public void AssignTeamsFFA()
    {
        foreach (PlayerData player in Players)
            player.team = -1;
    }

    [Rpc(SendTo.Server)]
    public void DealDamageServerRpc(ulong targetid, float damage, Vector3 force, Vector3 ragdollForce, RpcParams rpcParams = default) {
        DealDamage(targetid, rpcParams.Receive.SenderClientId, damage, force, ragdollForce);
    }

    public void DealDamage(ulong targetid, ulong senderId, float damage, Vector3 force, Vector3 ragdollForce) {
        PlayerData target = Players.Find(x => x.ClientId == targetid);
        PlayerData sender = Players.Find(x => x.ClientId == senderId);
        if(target == null || sender == null) return;

        if (senderId != targetid && sender.team >= 0 && sender.team == target.team) return;

        if(damageEnabled.Value) target.health -= damage;

        target.lastAttackedBy = senderId;
        target.lastAttackedTime = Time.time;

        if(force != Vector3.zero && (GameManager.instance == null || GameManager.instance.GameState == GameState.Lobby || GameManager.instance.currentGameMode.doPunching)) {
            target.player.RecieveForceClientRpc(force);
        }

        target.player.UpdateHealthClientRpc(target.health);

        if(target.health <= 0 && !target.isDead)
            Kill(target, sender, ragdollForce);

        print($"Player {targetid} took {damage} damage from Player {senderId}. Health now: {target.health}");
    }

    public void WorldDamage(ulong targetid, float damage, Vector3 ragdollForce)
    {
        PlayerData target = Players.Find(x => x.ClientId == targetid);
        if(target == null) return;

        target.health -= damage;
        target.player.UpdateHealthClientRpc(target.health);

        if(target.health > 0 || target.isDead) return;

        PlayerData killer = null;
        if (target.lastAttackedBy.HasValue && Time.time - target.lastAttackedTime < 20f)
        {
            int index = Players.FindIndex(x => x.ClientId == target.lastAttackedBy.Value);
            if (index >= 0) killer = Players[index];
        }

        Kill(target, killer, ragdollForce);
    }

    void Kill(PlayerData target, PlayerData killer, Vector3 ragdollForce)
    {
        target.isDead = true;
        target.deaths++;

        Vector3 pos = target.player.playerCharacter.transform.position;
        Quaternion rot = target.player.playerCharacter.transform.rotation;
        Vector3 vel = target.player.playerState.Velocity + ragdollForce;

        GameObject ragdoll = Instantiate(ragdollPrefab, pos, rot);
        NetworkObject ragdollNet = ragdoll.GetComponent<NetworkObject>();
        ragdollNet.Spawn();
        ragdoll.GetComponent<Ragdoll>().ApplyPoseAndVelocityClientRpc(target.player.NetworkObjectId, vel);

        target.player.DieClientRpc(ragdollNet.NetworkObjectId);

        if(GameManager.instance != null) GameManager.instance.worldObjects.Add(ragdoll);

        if (killer != null && killer != target)
        {
            killer.kills++;
            if(GameManager.instance != null) killer.score += GameManager.instance.currentGameMode.scoreOnKill;
        }

        target.lastAttackedBy = null;

        foreach(PlayerData _player in Players) {
            _player.player.ScoreboardUpdateClientRpc(target.ClientId, target.wins, target.kills, target.deaths);
            if (killer != null)
                _player.player.ScoreboardUpdateClientRpc(killer.ClientId, killer.wins, killer.kills, killer.deaths);

            string feed = killer != null && killer != target
                ? $"{killer.name}  >  {target.name}"
                : $"{target.name} died";
            bool highlight = _player.ClientId == target.ClientId || (killer != null && _player.ClientId == killer.ClientId);
            _player.player.AddKillfeedClientRpc(feed, highlight);
        }

        if (GameManager.instance != null && GameManager.instance.currentGameMode.respawnOnDeath && GameManager.instance.rooms.current.playersInRoom.Contains(target.playerGameObject)) {
            Revive(Players.FindIndex(x => x.ClientId == target.ClientId));
            GameManager.instance.GameTeleport(target.ClientId);
        }

        playersAlive = Players.Count(x => x.isDead == false);
    }

    [Rpc(SendTo.Server)]
    public void DEBUGRespawnServerRpc(ulong playerid, RpcParams rpcParams = default){
        int id = Players.FindIndex(x => x.ClientId == playerid);
        if(id < 0) return;
        Revive(id);

        if(GameManager.instance != null)
            GameManager.instance.GameTeleport(playerid);

        playersAlive = Players.Count(x => x.isDead == false);
    }

    void Revive(int id)
    {
        if(Players[id].isDead) {
            Players[id].isDead = false;
            Players[id].health = 100f;
            Players[id].player.RespawnClientRpc();
        }  else {
            Players[id].health = 100f;
            Players[id].player.UpdateHealthClientRpc(100f);
        }
    }

    public void RespawnEveryone()
    {
        for (int i = 0; i < Players.Count; i++)
        {
            Revive(i);
        }

        playersAlive = Players.Count(x => x.isDead == false);
    }

    //prevent telporting to same pos as anther player at same time
    readonly List<(Vector3 pos, float time)> recentTeleports = new List<(Vector3, float)>();
    const float teleportClearance = 1.5f;

    public void Teleport(ulong playerid, Vector3 position){
        int id = Players.FindIndex(x => x.ClientId == playerid);
        if(id < 0) return;

        recentTeleports.RemoveAll(t => Time.time - t.time > 1f);

        Vector3 anchor = position;
        for (int i = 1; i <= 16 && !IsClear(id, position); i++)
            position = anchor + Quaternion.Euler(0f, i * 137.5f, 0f) * Vector3.forward * (teleportClearance * (1f + i * 0.25f));

        recentTeleports.Add((position, Time.time));
        Players[id].player.TeleportClientRpc(position);
    }

    bool IsClear(int id, Vector3 position)
    {
        foreach (var t in recentTeleports)
            if (Vector3.Distance(t.pos, position) < teleportClearance) return false;

        for (int i = 0; i < Players.Count; i++)
        {
            if (i == id || Players[i].player == null) continue;
            if (Vector3.Distance(Players[i].player.playerCharacter.transform.position, position) < teleportClearance) return false;
        }
        return true;
    }

    public void ClearItem(ulong playerId, int itemId = -1)
    {
        int index = Players.FindIndex(x => x.ClientId == playerId);
        if(index < 0) return;

        Player player = Players[index].player;

        PlayerInventory inventory = player.playerInventory;
        if(inventory != null && inventory.NetworkIDInventory != null)
        {
            for(int i = 0; i < inventory.NetworkIDInventory.Count; i++)
            {
                ulong netId = inventory.NetworkIDInventory[i];

                if(netId == 0UL) continue;
                if(!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject netObj)) continue;

                if(GameManager.instance != null)
                    GameManager.instance.worldObjects.Remove(netObj.gameObject);

                if(netObj.IsSpawned)
                    netObj.Despawn(true);
            }
        }

        if(player.NetworkObject != null && player.NetworkObject.IsSpawned)
            player.ClearItemClientRpc(itemId);
    }

    public void GiveItem(int itemId, ulong playerId)
    {
        int id = Players.FindIndex(x => x.ClientId == playerId);
        if(id < 0) return;
        NetworkObject netObj = GameManager.instance.SpawnItem(itemId);
        Players[id].player.GiveItemClientRpc(netObj.NetworkObjectId);
    }

    [Rpc(SendTo.Server)]
    public void SwapItemServerRpc(ulong targetId, int itemId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if(senderId == targetId) return;

        ClearItem(senderId, itemId);
        GiveItem(itemId, targetId);
    }

    public void UpdatePlayerScoreboard(ulong playerId) {
        PlayerData targetPlayer = Players.Find(x => x.ClientId == playerId);
        if(targetPlayer == null) return;
        foreach(PlayerData _player in Players) {
            _player.player.ScoreboardUpdateClientRpc(targetPlayer.ClientId, targetPlayer.wins, targetPlayer.kills, targetPlayer.deaths);
        }
    }

    ulong lastCrownLeader = ulong.MaxValue;
    bool lastCrownActive;

    public void UpdateCrowns(bool active)
    {
        int maxScore = 0;
        PlayerData leader = null;

        foreach (PlayerData p in Players)
        {
            if (p.score > maxScore)
            {
                maxScore = p.score;
                leader = p;
            }
        }

        bool show = active && maxScore > 0 && leader != null;
        ulong leaderId = show ? leader.ClientId : ulong.MaxValue;
        if (show == lastCrownActive && leaderId == lastCrownLeader) return;

        lastCrownActive = show;
        lastCrownLeader = leaderId;

        foreach (PlayerData p in Players)
            p.player.SetCrownClientRpc(show && p == leader);
    }
}

[System.Serializable]
public class PlayerData
{
    public PlayerData(GameObject GO, ulong id, float hp, string _name)
    {
        playerGameObject = GO;
        player = GO.GetComponent<Player>();
        ClientId = id;
        health = hp;
        name = _name;
    }

    public GameObject playerGameObject;
    public Player player;
    public ulong ClientId;
    public string name;
    public int kills, deaths, wins, score;
    public float health;
    public bool isDead = false;
    public int team = -1;
    public ulong? lastAttackedBy;
    public float lastAttackedTime;
}
