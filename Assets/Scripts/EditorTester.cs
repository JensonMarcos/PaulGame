using UnityEngine;
using Unity.Netcode;

public class EditorTester : NetworkBehaviour
{
    [SerializeField] GameObject networkManagerGameObject;
    [SerializeField] GameObject playerManagerGameObject;

    
    void Awake()
    {
        #if !UNITY_EDITOR
            Destroy(gameObject);
        #endif

        if(SteamManager.Instance == null)
            Instantiate(networkManagerGameObject);
        else 
            Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if(!IsHost || (SteamManager.Instance != null)) return;

        GameObject player = Instantiate(playerManagerGameObject);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId, true);
    }
    
}
