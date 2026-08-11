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
    public bool crateLootEnabled = true;

    [SerializeField] GameObject itemCratePrefab;
    [SerializeField] Transform itemCrateSpawns;
    
    public void Initialize() {
        if(!IsServer) return;

        crateLootEnabled = true;
        
        foreach (SpawnObject spawnObject in objectsToSpawn) {
            if (spawnObject.prefab == null || spawnObject.point == null) continue;
            GameObject obj = Instantiate(spawnObject.prefab, spawnObject.point.position, spawnObject.point.rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            GameManager.instance.worldObjects.Add(obj);
        }

        if(itemCrateSpawns != null) {
            foreach (Transform spawnPoint in itemCrateSpawns.transform) {
                GameObject obj = Instantiate(itemCratePrefab, spawnPoint.position, spawnPoint.rotation);
                obj.GetComponent<NetworkObject>().Spawn(true);
                ItemCrate crate = obj.GetComponent<ItemCrate>();
                if (crate != null) crate.room = this;
                GameManager.instance.worldObjects.Add(obj);
            }
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
                if (IsPlaying(doorEnter, "DoorOpen")) doorEnter.Play("DoorClose");
                if (IsPlaying(doorExit, "DoorOpen")) doorExit.Play("DoorClose");
                break;
        }
    }

    static bool IsPlaying(Animator animator, string clipName)
    {
        if (animator == null) return false;

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        return clips.Length > 0 && clips[0].clip != null && clips[0].clip.name == clipName;
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
