using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Room : NetworkBehaviour
{
    //public GameObject enterDoor, exitDoor;
    public Transform spawnPoint, objectivePoint;

    public GameObject[] objectsToSpawn;
    public Transform[] objectSpawnPoints;

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
        for (int i = 0; i < objectsToSpawn.Length; i++) {
            GameObject obj = Instantiate(objectsToSpawn[i], objectSpawnPoints[i].position, objectSpawnPoints[i].rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            GameManager.instance.worldObjects.Add(obj);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DoorClientRpc(float state, float time) {
        StartCoroutine(ChangeDoorState(state, time * GameMode.animTimeMult));
    }
    
    IEnumerator ChangeDoorState(float target, float time) {
        float t = 0f;
        float start = anim.GetFloat("OpenState");
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
