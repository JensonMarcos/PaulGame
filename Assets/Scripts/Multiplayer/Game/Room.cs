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

    public GameMode roomGameMode;
    
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

    [ClientRpc]
    public void DoorClientRpc(float open, float speed) {
        // if(doorNum == 0) {
        //     enterDoor.SetActive(open);
        // } else {
        //     exitDoor.SetActive(open);
        // }

        openState = open;
        openSpeed = speed;
    }

    void Update()
    {
        anim.SetFloat("OpenState", Mathf.Lerp(anim.GetFloat("OpenState"), openState, Time.deltaTime * openSpeed));
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player") && other.GetComponent<NetworkObject>()) {
            if(!playersInRoom.Contains(other.gameObject)) {
                playersInRoom.Add(other.gameObject);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player") && other.GetComponent<NetworkObject>()) {
            if(playersInRoom.Contains(other.gameObject)) {
                playersInRoom.Remove(other.gameObject);
            }
        }
    }
}
