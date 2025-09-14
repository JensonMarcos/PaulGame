using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SceneManager : NetworkBehaviour
{
    public static SceneManager instance;
    
    public GameObject playerManagerGameObject;

    void Awake() {
        if(instance) {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        instance = this;

    }

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SceneLoaded;
    }

    private void SceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if(IsHost && sceneName == "Level1") {
            GameObject player = Instantiate(playerManagerGameObject);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId, true);

        }
    }
}
