using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum doorState
{
    enter, //0
    exit, //1
    closed //0.5
}

[System.Serializable]
public struct SpawnObject
{
    public GameObject prefab;
    public Transform point;
}

public class Room : NetworkBehaviour
{
    public Transform nextRoomPoint;
    public Transform respawnPoint;
    public Transform moveSpawnPoint;
    public Animator doorEnter;
    public Animator doorExit;

    public List<SpawnObject> objectsToSpawn;

    public List<GameObject> playersInRoom;

    public GameMode GameMode;
    
    void Start() {
        if(!IsServer) return;
        foreach (SpawnObject spawnObject in objectsToSpawn) {
            if (spawnObject.prefab == null || spawnObject.point == null) continue;
            GameObject obj = Instantiate(spawnObject.prefab, spawnObject.point.position, spawnObject.point.rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            GameManager.instance.worldObjects.Add(obj);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DoorClientRpc(doorState state) {
        switch(state) {
            case doorState.enter:
                doorEnter.Play("DoorOpen");
                break;
            case doorState.exit:
                doorExit.Play("DoorOpen");
                break;
            case doorState.closed:
                doorEnter.Play("DoorClose");
                break;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        GameObject player = other.transform.root.gameObject;
        if(player.CompareTag("Player") && player.GetComponent<NetworkObject>()) {
            if(!playersInRoom.Contains(player)) {
                playersInRoom.Add(player);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        GameObject player = other.transform.root.gameObject;
        if(player.CompareTag("Player") && player.GetComponent<NetworkObject>()) {
            if(playersInRoom.Contains(player)) {
                playersInRoom.Remove(player);
            }
        }
    }

    float easeInOutQuad(float x) {
        return x < 0.5 ? 2 * x * x : 1 - Mathf.Pow(-2 * x + 2, 2) / 2;
    }
}
