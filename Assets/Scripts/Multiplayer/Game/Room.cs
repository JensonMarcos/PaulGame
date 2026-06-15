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
    //public GameObject enterDoor, exitDoor;
    public Transform roomSpawnPoint;
    public Transform respawnPoint, moveSpawnPoint;

    public List<SpawnObject> objectsToSpawn;

    public Animator anim;
    float openState = 0.5f;
    public float openSpeed = 5f;

    public List<GameObject> playersInRoom;

    public GameMode GameMode;
    
    void Start() {
        anim = GetComponent<Animator>();
        openState = 0.5f;
        anim.SetFloat("OpenState", openState);

        if(!IsServer) return;
        foreach (SpawnObject spawnObject in objectsToSpawn) {
            if (spawnObject.prefab == null || spawnObject.point == null) continue;
            GameObject obj = Instantiate(spawnObject.prefab, spawnObject.point.position, spawnObject.point.rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            GameManager.instance.worldObjects.Add(obj);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DoorClientRpc(doorState state, float time) {
        StartCoroutine(ChangeDoorState(state, time * GameMode.animTimeMult));
    }
    
    IEnumerator ChangeDoorState(doorState state, float time) {
        float t = 0f;
        float start = anim.GetFloat("OpenState");
        float target = state == doorState.enter ? 0f : state == doorState.exit ? 1f : 0.5f;
        while(t < 1f) {
            t += Time.deltaTime / time;
            anim.SetFloat("OpenState", Mathf.Lerp(start, target, easeInOutQuad(t)));
            yield return null;
        }
        anim.SetFloat("OpenState", target);
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
