using System;
using System.Collections.Generic;
using System.Linq;
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

    public override void OnNetworkSpawn() {
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

    private void OnClientConnectedCallback(ulong id)
    {
        if(!IsServer) return;
        SpawnPlayer(id);
    }

    // void Start() {
    //     if(!IsOwner || !IsServer) return;
    //     instance = this;
    //     List<ulong>clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
    //     print(clients.Count);
    //     foreach(ulong id in clients) {
    //         SpawnPlayer(id);
    //     }
    // }

    void SpawnPlayer(ulong id) {
        foreach(PlayerData _player in Players) {
            if(_player.ClientId == id) return;
        }
        GameObject player = Instantiate(playerPrefab);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
        Players.Add(new PlayerData(player, id, 100f, "Player"+id));

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

    [Rpc(SendTo.Server)]
    public void DealDamageServerRpc(ulong targetid, float damage, Vector3 force, RpcParams rpcParams = default) {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PlayerData target = Players[Players.FindIndex(x => x.ClientId == targetid)];
        PlayerData sender = Players[Players.FindIndex(x => x.ClientId == senderId)];

        if(damageEnabled.Value || damage == 1234f) target.health -= damage;

        if(force != Vector3.zero) {
            target.playerGameObject.GetComponent<Player>().RecieveForceClientRpc(force);
        }

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
                if(GameManager.instance != null) target.playerGameObject.GetComponent<Player>().TeleportClientRpc(GameManager.instance.currentRoom.objectivePoint.position);
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

        //Players[itarget].playerGameObject.GetComponent<Player>().UpdateHealthClientRpc(Players[itarget].health);

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
        } 

        playersAlive = Players.Count(x => x.isDead == false);
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
}
