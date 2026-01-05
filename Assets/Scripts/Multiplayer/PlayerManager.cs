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

    public override void OnNetworkSpawn() {
        instance = this;
        
        if(!IsServer) return;
        
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;

        List<ulong>clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        print(clients.Count);
        foreach(ulong id in clients) {
            SpawnPlayer(id);
        }
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
        Players.Add(new PlayerData(player, id, 100f));

        playersAlive = Players.Count(x => x.isDead == false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DealDamageServerRpc(ulong targetid, float damage, ServerRpcParams serverRpcParams = default) {
        ulong senderId = serverRpcParams.Receive.SenderClientId;
        int itarget = Players.FindIndex(x => x.ClientId == targetid);
        int isender = Players.FindIndex(x => x.ClientId == senderId);
        Players[itarget].health -= damage;

        if(Players[itarget].health <= 0 && !Players[itarget].isDead) {
            Players[itarget].isDead = true;

            Players[itarget].deaths++;
            Players[isender].kills++;

            Players[isender].score += 100;

            //Vector3 pos = Players[itarget].playerGameObject.transform.position;
            //Quaternion rot = Players[itarget].playerGameObject.GetComponent<MovementController>().bodyTransform.rotation;
            //Vector3 vel = Players[itarget].playerGameObject.GetComponent<Rigidbody>().linearVelocity;

            //GameObject ragdoll = Instantiate(ragdollPrefab, pos, rot);
            //ragdoll.GetComponent<NetworkObject>().Spawn();
            //GameManager.instance.worldObjects.Add(ragdoll);

            //Players[itarget].health = 100f;
            //Vector3 teleport = damage == 1234 ? GameManager.instance.currentRoom.objectivePoint.position : Vector3.zero;
            Players[itarget].playerGameObject.GetComponent<Player>().DieClientRpc();

            //ragdoll.transform.position = pos;
            //ragdoll.transform.rotation = rot;
            //ragdoll.GetComponent<Rigidbody>().linearVelocity = vel; 
        }

        //Players[itarget].playerGameObject.GetComponent<Player>().UpdateHealthClientRpc(Players[itarget].health);

        playersAlive = Players.Count(x => x.isDead == false);

        print($"Player {targetid} took {damage} damage from {senderId}. Health now: {Players[itarget].health}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void RespawnServerRpc(ulong playerid, ServerRpcParams serverRpcParams = default){
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
    public PlayerData(GameObject GO, ulong id, float hp)
    {
        playerGameObject = GO;
        ClientId = id;
        health = hp;
    }

    public GameObject playerGameObject;
    public ulong ClientId;
    public string name;
    public int kills, deaths, wins, score;
    public float health;
    public bool isDead = false;
}
