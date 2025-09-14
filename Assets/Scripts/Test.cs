using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Test : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(Connected());
    }

    IEnumerator Connected() {
        yield return new WaitUntil(() => NetworkManager.Singleton != null);
        print("Connected");
    }
}
