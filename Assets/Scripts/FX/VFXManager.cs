using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.Pool;

public class VFXManager : NetworkBehaviour
{
    public static VFXManager instance;

    [Header("Shoot FX")]
    [Space]
    [Header("Bullet Trail")]
    [SerializeField] GameObject bulletTrailPrefab;
    [SerializeField] float trailSpeed = 600f;
    ObjectPool<GameObject> bulletTrailPool;

    [Header("Muzzle Flash")]
    [SerializeField] GameObject muzzleFlashPrefab;
    [SerializeField] float muzzleFlashDuration = 0.1f;
    ObjectPool<GameObject> muzzleFlashPool;

    [Header("Hit Decals")]
    [SerializeField] GameObject[] hitDecals;
    [SerializeField] float decalDuration = 5f;
    ObjectPool<GameObject>[] decalPools;

    [Header("Projectiles")]
    [SerializeField] GameObject[] projectiles;
    [SerializeField] LayerMask projectileHitMask;
    ObjectPool<GameObject>[] projectilePools;

    [Header("Explosion")]
    [SerializeField] GameObject explosionPrefab;
    [SerializeField] float explosionDuration = 2f;
    ObjectPool<GameObject> explosionPool;

    void Awake() {
        instance = this;

        bulletTrailPool = CreatePool(bulletTrailPrefab, 250, 2000);
        muzzleFlashPool = CreatePool(muzzleFlashPrefab, 25, 200);
        explosionPool = CreatePool(explosionPrefab, 20, 100);

        decalPools = new ObjectPool<GameObject>[hitDecals.Length];
        for(int i = 0; i < hitDecals.Length; i++) {
            decalPools[i] = CreatePool(hitDecals[i], 500, 4000);
        }

        projectilePools = new ObjectPool<GameObject>[projectiles.Length];
        for(int i = 0; i < projectiles.Length; i++) {
            projectilePools[i] = CreatePool(projectiles[i], 20, 100);
        }
    }

    ObjectPool<GameObject> CreatePool(GameObject prefab, int capacity, int max) {
        return new ObjectPool<GameObject>(() => {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            return obj;
        }, actionOnGet: (obj) => {
            obj.SetActive(true);
        }, actionOnRelease: (obj) => {
            obj.SetActive(false);
            obj.transform.SetParent(transform);
        }, actionOnDestroy: (obj) => {
            if(obj == null) return;
            if(Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }, collectionCheck: false, defaultCapacity: capacity, maxSize: max);
    }

    public void PlayTrail(Vector3 startPos, Vector3 endPos)
    {
        StartCoroutine(SpawnTrail(startPos, endPos));
    }

    public void PlayMuzzleFlash(Vector3 startPos)
    {
        StartCoroutine(SpawnMuzzleFlash(startPos));
    }

    public void PlayDecal(Vector3 pos, Vector3 hitNormal, int decal)
    {
        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal : Vector3.up;
        StartCoroutine(SpawnDecal(decal, pos, Quaternion.LookRotation(safeNormal)));
    }

    public void PlayExplosion(Vector3 position)
    {
        StartCoroutine(SpawnExplosion(position));
        ExplosionFXServerRpc(position);
    }

    IEnumerator SpawnDecal(int decal, Vector3 pos, Quaternion rot) {
        GameObject _decal = decalPools[decal].Get();

        _decal.transform.SetPositionAndRotation(pos, rot);

        if(decal == 0) { //REALLY BAD HARDCODED
            Collider[] colliders = Physics.OverlapSphere(pos, 0.2f);
            if(colliders.Length != 0) {
                _decal.transform.SetParent(colliders[0].transform, true);
            }
        }

        yield return new WaitForSeconds(decalDuration);

        //parent may have been destroyed taking the decal with it
        if(_decal != null) decalPools[decal].Release(_decal);
    }
    
    IEnumerator SpawnTrail(Vector3 startPos, Vector3 endPos) {
        GameObject _trail = bulletTrailPool.Get();
        _trail.transform.position = startPos;
        if(_trail.TryGetComponent(out TrailRenderer _renderer)) _renderer.Clear();

        for(float t = 0; t < 1;) {
            _trail.transform.position = Vector3.Lerp(startPos, endPos, t);
            t += trailSpeed * Time.deltaTime / Vector3.Distance(startPos, endPos);

            yield return null;
        }
        bulletTrailPool.Release(_trail);
    }

    IEnumerator SpawnMuzzleFlash(Vector3 startPos) {
        GameObject _flash = muzzleFlashPool.Get();
        _flash.transform.position = startPos;

        yield return new WaitForSeconds(muzzleFlashDuration);
        muzzleFlashPool.Release(_flash);
    }

    IEnumerator SpawnExplosion(Vector3 position) {
        GameObject explosion = explosionPool.Get();
        explosion.transform.position = position;

        yield return new WaitForSeconds(explosionDuration);
        explosionPool.Release(explosion);
    }

    IEnumerator SpawnProjectile(int id, Vector3 position, Vector3 direction, float speed, float gravity, float size, float lifetime) {
        GameObject obj = projectilePools[id].Get();
        obj.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));

        Projectile projectile = obj.GetComponent<Projectile>();
        projectile.Launch(direction, speed, gravity, size, lifetime, projectileHitMask);

        while (projectile.IsAlive)
            yield return null;

        projectile.ResetProjectile();
        if (obj != null) projectilePools[id].Release(obj);
    }

    public void ProjectileFX(int id, Vector3 position, Vector3 direction, float speed, float gravity, float size, float lifetime) {
        StartCoroutine(SpawnProjectile(id, position, direction, speed, gravity, size, lifetime));
        ProjectileFXServerRpc(id, position, direction, speed, gravity, size, lifetime);
    }

    [Rpc(SendTo.Server)]
    public void ProjectileFXServerRpc(int id, Vector3 position, Vector3 direction, float speed, float gravity, float size, float lifetime, RpcParams rpcParams = default) {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        ProjectileFXClientRpc(id, position, direction, speed, gravity, size, lifetime, RpcTarget.Not(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ProjectileFXClientRpc(int id, Vector3 position, Vector3 direction, float speed, float gravity, float size, float lifetime, RpcParams rpcParams = default) {
        StartCoroutine(SpawnProjectile(id, position, direction, speed, gravity, size, lifetime));
    }

    public void MuzzleFlashFX(Vector3 startPos) {
        StartCoroutine(SpawnMuzzleFlash(startPos));
        MuzzleFlashFXServerRpc(startPos);
    }

    [Rpc(SendTo.Server)]
    public void MuzzleFlashFXServerRpc(Vector3 startPos, RpcParams rpcParams = default) {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        MuzzleFlashFXClientRpc(startPos, RpcTarget.Not(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void MuzzleFlashFXClientRpc(Vector3 startPos, RpcParams rpcParams = default) {
        StartCoroutine(SpawnMuzzleFlash(startPos));
    }

    [Rpc(SendTo.Server)]
    public void ExplosionFXServerRpc(Vector3 position, RpcParams rpcParams = default) {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        ExplosionFXClientRpc(position, RpcTarget.Not(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void ExplosionFXClientRpc(Vector3 position, RpcParams rpcParams = default) {
        StartCoroutine(SpawnExplosion(position));
    }
}
