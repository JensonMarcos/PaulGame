using Unity.Netcode;
using UnityEngine;

public class NetworkButtons : MonoBehaviour
{

    public GameObject cam;
    void OnGUI() {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if(!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer) {
            if(GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
            if(GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
            if(GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
        }

        GUILayout.EndArea();
    }
    void Update() {
        if(NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) {
            Destroy(cam);
            Destroy(this);
        }
    }
}
