using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager instance;
    public GameObject playerPrefab, ragdollPrefab;
    public List<PlayerData> allplayers = new List<PlayerData>();
    public int playersAlive = 0;

    public override void OnNetworkSpawn() {
        if(!IsOwner || !IsServer) Destroy(this);
        instance = this;
        NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
    }

    
    void OnDisable() {
        if(!IsOwner || !IsServer) return;
        NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
    }

    private void OnClientConnectedCallback(ulong id)
    {
        if(!IsOwner || !IsServer) return;
        SpawnPlayer(id);
    }

    void Start() {
        if(!IsOwner || !IsServer) return;
        instance = this;
        List<ulong>clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        print(clients.Count);
        foreach(ulong id in clients) {
            SpawnPlayer(id);
        }
    }

    void SpawnPlayer(ulong id) {
        bool repeat = false;
        foreach(PlayerData _player in allplayers) {
            if(_player.ID == id) {
                repeat = true;
            }
        }
        if(repeat) return;
        GameObject player = Instantiate(playerPrefab);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
        allplayers.Add(new PlayerData(player, id, 100f));

        playersAlive = allplayers.Count(x => x.isDead == false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DealDamageServerRpc(ulong targetid, float damage, ServerRpcParams serverRpcParams = default) {
        ulong senderId = serverRpcParams.Receive.SenderClientId;
        int itarget = allplayers.FindIndex(x => x.ID == targetid);
        int isender = allplayers.FindIndex(x => x.ID == senderId);
        allplayers[itarget].health -= damage;

        if(allplayers[itarget].health <= 0 && !allplayers[itarget].isDead) {
            allplayers[itarget].isDead = true;

            allplayers[itarget].deaths++;
            allplayers[isender].kills++;

            allplayers[isender].score += 100;

            Vector3 pos = allplayers[itarget].playerGameObject.transform.position;
            quaternion rot = allplayers[itarget].playerGameObject.GetComponent<MovementController>().bodyTransform.rotation;
            Vector3 vel = allplayers[itarget].playerGameObject.GetComponent<Rigidbody>().linearVelocity;

            GameObject ragdoll = Instantiate(ragdollPrefab, pos, rot);
            ragdoll.GetComponent<NetworkObject>().Spawn();
            GameManager.instance.worldObjects.Add(ragdoll);

            //allplayers[itarget].health = 100f;
            Vector3 teleport = damage == 1234 ? GameManager.instance.currentRoom.objectivePoint.position : Vector3.zero;
            allplayers[itarget].playerGameObject.GetComponent<Health>().DieClientRpc(teleport);

            ragdoll.transform.position = pos;
            ragdoll.transform.rotation = rot;
            ragdoll.GetComponent<Rigidbody>().linearVelocity = vel; 
        }

        allplayers[itarget].playerGameObject.GetComponent<Health>().UpdateHealthClientRpc(allplayers[itarget].health);
        //DealDamageClientRpc(id, damage);

        playersAlive = allplayers.Count(x => x.isDead == false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RespawnServerRpc(ulong playerid, ServerRpcParams serverRpcParams = default){
        int id = allplayers.FindIndex(x => x.ID == playerid);
        if(allplayers[id].isDead) {
            allplayers[id].isDead = false;
            allplayers[id].health = 100f;
            allplayers[id].playerGameObject.GetComponent<Health>().RespawnClientRpc();
        } 

        playersAlive = allplayers.Count(x => x.isDead == false);
    }

    void Update()
    {
        //playersAlive = allplayers.Count(x => x.isDead == false);
    }
}

[System.Serializable]
public class PlayerData
{
    public PlayerData(GameObject GO, ulong id, float hp)
    {
        playerGameObject = GO;
        ID = id;
        health = hp;
    }

    public GameObject playerGameObject;
    public ulong ID;
    public string name;
    public int kills, deaths, wins, score;
    public float health, timeTillRegen, regenTime = 4f;
    public bool isDead = false;
}
