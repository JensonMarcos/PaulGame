using UnityEngine;
using Unity.Netcode;
using System.Linq;
using System.Collections;

public class GameFX : NetworkBehaviour
{
    public static GameFX instance;
    [SerializeField] GameObject bulletTrailPrefab;
    [SerializeField] float trailSpeed = 600f;
    [SerializeField] GameObject muzzleFlashPrefab;
    [SerializeField] GameObject[] hitDecals;

    void Awake() {
        instance = this;
    }

    public void LocalShootFX(Vector3 startPos, Vector3 endPos, Vector3 hitNormal, bool didHit, bool doTrail, int decal) {
        ShootFX(startPos, endPos, hitNormal, didHit, doTrail, decal);
        ShootFXServerRpc(startPos, endPos, hitNormal, didHit, doTrail, decal);
    }
    
    [ServerRpc(RequireOwnership = false)] 
    public void ShootFXServerRpc(Vector3 startPos, Vector3 endPos, Vector3 hitNormal, bool didHit, bool doTrail, int decal, ServerRpcParams serverRpcParams = default) {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        ClientRpcParams clientRpcParams = new ClientRpcParams {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = NetworkManager.Singleton.ConnectedClientsIds.Where(id => id != senderClientId).ToList() // Exclude sender
            }
        };
        ShootFXClientRpc(startPos, endPos, hitNormal, didHit, doTrail, decal, clientRpcParams);
    }

    [ClientRpc] 
    public void ShootFXClientRpc(Vector3 startPos, Vector3 endPos, Vector3 hitNormal, bool didHit, bool doTrail, int decal, ClientRpcParams clientRpcParams = default) {
        ShootFX(startPos, endPos, hitNormal, didHit, doTrail, decal);
    }

    public void ShootFX(Vector3 startPos, Vector3 endPos, Vector3 hitNormal, bool didHit, bool doTrail, int decal) {
        //muzzle flash
        GameObject _flash = Instantiate(muzzleFlashPrefab, startPos, Quaternion.identity);
        Destroy(_flash, 1f);

        //trail
        if(doTrail) StartCoroutine(SpawnTrail(startPos, endPos));

        if(!didHit) return;

        //Decals, hit fx
        GameObject impactGO = Instantiate(hitDecals[decal], endPos, Quaternion.LookRotation(hitNormal));
        Collider[] colliders = Physics.OverlapSphere(endPos, 0.2f);
        if(colliders.Length != 0) {
            impactGO.transform.parent = colliders[0].transform;
        }
    }
    
    public IEnumerator SpawnTrail(Vector3 startPos, Vector3 endPos) {
        GameObject _trail = Instantiate(bulletTrailPrefab, startPos, Quaternion.identity);
        //float time = 0;
        Vector3 trailStart = _trail.transform.position;
        
        for(float t = 0; t < 1;) {
            _trail.transform.position = Vector3.Lerp(trailStart, endPos, t);
            t += trailSpeed * Time.deltaTime / Vector3.Distance(trailStart, endPos);

            yield return null;
        }
        Destroy(_trail.gameObject);
    }
}
