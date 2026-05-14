using System;
using System.Collections.Generic;
using System.Linq;
using Netcode.Transports.Facepunch;
using Steamworks;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager instance;
    public GameObject playerPrefab, ragdollPrefab;
    public List<PlayerData> Players = new List<PlayerData>();
    public int playersAlive = 0;

    public NetworkVariable<bool> damageEnabled = new();
    public bool reloadEnabled = true;

    public override void OnNetworkSpawn() {
        // if(instance) {
        //     Destroy(gameObject);
        //     return;
        // }
        instance = this;
        
        if(!IsServer) return;
        
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;

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
    }

    void OnClientConnectedCallback(ulong id)
    {
        if(!IsServer) return;
        SpawnPlayer(id);
    }

    void SpawnPlayer(ulong id) {
        foreach(PlayerData _player in Players) {
            if(_player.ClientId == id) return;
        }
        GameObject player = Instantiate(playerPrefab);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
        Players.Add(new PlayerData(player, id, 100f, GetPlayerName(id)));

        playersAlive = Players.Count(x => x.isDead == false);

        //add existing players to scoreboard of new player, retarded ass code
        foreach(PlayerData _player in Players) {
            if(_player.ClientId == id) continue;
            Players[Players.FindIndex(x => x.ClientId == id)].playerGameObject.GetComponent<Player>().AddOrRemoveScoreboardItemClientRpc(true, _player.ClientId, _player.name, _player.wins, _player.kills, _player.deaths);
        }

        //add new player to scoreboards of all players (including self)
        foreach(PlayerData _player in Players) {
            _player.playerGameObject.GetComponent<Player>().AddOrRemoveScoreboardItemClientRpc(true, id, Players[Players.FindIndex(x => x.ClientId == id)].name, 0, 0, 0);
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
    public void DealDamageServerRpc(ulong targetid, float damage, Vector3 force, RpcParams rpcParams = default) {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PlayerData target = Players[Players.FindIndex(x => x.ClientId == targetid)];
        PlayerData sender = Players[Players.FindIndex(x => x.ClientId == senderId)];

        if (damage != 1234f && sender.team >= 0 && sender.team == target.team) return;

        if(damageEnabled.Value || damage == 1234f) target.health -= damage;

        if(force != Vector3.zero && (GameManager.instance == null || GameManager.instance.currentGameMode.doPunching)) {
            target.playerGameObject.GetComponent<Player>().RecieveForceClientRpc(force);
        }

        target.playerGameObject.GetComponent<Player>().UpdateHealthClientRpc(target.health);

        if(target.health <= 0 && !target.isDead) { //player dies
            target.isDead = true;

            target.deaths++;

            Vector3 pos = target.playerGameObject.GetComponent<Player>().playerCharacter.transform.position;
            Quaternion rot = target.playerGameObject.GetComponent<Player>().playerCharacter.transform.rotation;
            Vector3 vel = target.playerGameObject.GetComponent<Player>().playerState.Velocity;

            target.playerGameObject.GetComponent<Player>().DieClientRpc();

            GameObject ragdoll = Instantiate(ragdollPrefab, pos, rot);
            ragdoll.GetComponent<NetworkObject>().Spawn();
            ragdoll.GetComponent<NetworkProp>().ApplyForceServerRpc(vel, ragdollPrefab.transform.position);

            if(GameManager.instance != null) GameManager.instance.worldObjects.Add(ragdoll);

            //self kill
            if(damage == 1234f)
            {
                //if(GameManager.instance != null) target.playerGameObject.GetComponent<Player>().TeleportClientRpc(GameManager.instance.rooms.current.objectivePoint.position);
            } else
            {
                sender.kills++;
                sender.score += 100;
            }

            foreach(PlayerData _player in Players) {
                _player.playerGameObject.GetComponent<Player>().ScoreboardUpdateClientRpc(target.ClientId, target.wins, target.kills, target.deaths);
                _player.playerGameObject.GetComponent<Player>().ScoreboardUpdateClientRpc(sender.ClientId, sender.wins, sender.kills, sender.deaths);

                _player.playerGameObject.GetComponent<Player>().AddKillfeedClientRpc($"{sender.name}  >  {target.name}", _player.ClientId == sender.ClientId || _player.ClientId == target.ClientId);
            }
        }

        playersAlive = Players.Count(x => x.isDead == false);

        print($"Player {targetid} took {damage} damage from Player {senderId}. Health now: {target.health}");
    }

    [Rpc(SendTo.Server)]
    public void RespawnServerRpc(ulong playerid, RpcParams rpcParams = default){
        int id = Players.FindIndex(x => x.ClientId == playerid);
        if(Players[id].isDead) {
            Players[id].isDead = false;
            Players[id].health = 100f;
            Players[id].playerGameObject.GetComponent<Player>().RespawnClientRpc();
        }  else {
            Players[id].health = 100f;
            Players[id].playerGameObject.GetComponent<Player>().UpdateHealthClientRpc(100f);
        }

        playersAlive = Players.Count(x => x.isDead == false);
    }

    public void RespawnEveryone()
    {
        foreach (PlayerData player in Players)
        {
            RespawnServerRpc(player.ClientId);
        }
    }

    [Rpc(SendTo.Server)]
    public void TeleportServerRpc(ulong playerid, Vector3 position, RpcParams rpcParams = default){
        int id = Players.FindIndex(x => x.ClientId == playerid);
        Players[id].playerGameObject.GetComponent<Player>().TeleportClientRpc(position);
    }

    [Rpc(SendTo.Server)]
    public void ClearItemServerRpc(int itemId = -1, RpcParams rpcParams = default){ //prolly switch this to clearing a specific player not all 
        for(int i = 0; i < Players.Count; i++) {
            Players[i].playerGameObject.GetComponent<Player>().ClearItemClientRpc(itemId);
        }
    }

    [Rpc(SendTo.Server)]
    public void GiveItemServerRpc(int itemId, ulong playerId, RpcParams rpcParams = default)
    {
        NetworkObject netObj = GameManager.instance.SpawnItem(itemId);
        int id = Players.FindIndex(x => x.ClientId == playerId);
        Players[id].playerGameObject.GetComponent<Player>().GiveItemClientRpc(netObj.NetworkObjectId);
    }
}

[System.Serializable]
public class PlayerData
{
    public PlayerData(GameObject GO, ulong id, float hp, string _name)
    {
        playerGameObject = GO;
        ClientId = id;
        health = hp;
        name = _name;
    }

    public GameObject playerGameObject;
    public ulong ClientId;
    public string name;
    public int kills, deaths, wins, score;
    public float health;
    public bool isDead = false;
    public int team = -1;
}
