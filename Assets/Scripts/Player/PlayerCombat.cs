using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct CombatInputs
{
    public bool Attack;
    public bool Aim;
    public bool Reload;
}

public struct ShotPellet : INetworkSerializable
{
    public Vector3 end;
    public Vector3 normal;
    public bool hit;
    public bool trail;
    public int decal;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref end);
        serializer.SerializeValue(ref normal);
        serializer.SerializeValue(ref hit);
        serializer.SerializeValue(ref trail);
        serializer.SerializeValue(ref decal);
    }
}

public struct ShotFx : INetworkSerializable
{
    public Vector3 muzzle;
    public Vector3 recoil;
    public float backKick;
    public float rotKick;
    public bool playRecoil;
    public bool doMuzzle;
    public ShotPellet[] pellets;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref muzzle);
        serializer.SerializeValue(ref recoil);
        serializer.SerializeValue(ref backKick);
        serializer.SerializeValue(ref rotKick);
        serializer.SerializeValue(ref playRecoil);
        serializer.SerializeValue(ref doMuzzle);

        int count = pellets == null ? 0 : pellets.Length;
        serializer.SerializeValue(ref count);
        if (serializer.IsReader) pellets = new ShotPellet[count];
        for (int i = 0; i < count; i++)
        {
            ShotPellet pellet = pellets[i];
            pellet.NetworkSerialize(serializer);
            pellets[i] = pellet;
        }
    }
}

public class PlayerCombat : NetworkBehaviour
{
    public float Aiming;

    public float Reloading;

    //[SerializeField] PlayerInventory inventory;
    [SerializeField] PlayerCharacter character;
    [SerializeField] PlayerAnimations animations;
    [SerializeField] Transform cam;

    [SerializeField] float aimSpeed;
    [SerializeField] LayerMask shootLayer;

    [SerializeField] float upForceMult = 0.5f;

    [SerializeField] int playerHitDecalIndex;
    [SerializeField] int crateHitDecalIndex;

    [SerializeField] SoundData hitSound;

    bool wishAttack;
    bool wishAim;
    bool wishReload;

    float nextTimeToFire;

    ItemClient prevItem;
    Player player;
    Coroutine reloadCoroutine;

    readonly RaycastHit[] shootHitsBuffer = new RaycastHit[16];
    readonly Collider[] explosionOverlapBuffer = new Collider[32];
    readonly HashSet<ulong> explosionHitNetIds = new HashSet<ulong>();
    readonly List<ShotPellet> shotPellets = new List<ShotPellet>(16);

    void Awake()
    {
        player = transform.root.GetComponent<Player>();
    }

    public void SetInputs(CombatInputs inputs, bool _sprinting, bool _readyPull)
    {
        wishAttack = inputs.Attack;

        wishAim = inputs.Aim;
        if (_sprinting || !_readyPull || Reloading > 0) {
            wishAttack = false;
            wishAim = false;
        }

        wishReload = inputs.Reload;         
        if(!_readyPull || Reloading > 0) wishReload = false;
    }

    public void UpdateCombat(PlayerState _state, ItemClient _item)
    {
        if(_item.data.type is ItemType.Sniper && nextTimeToFire > Time.time) wishAim = false; 

        Aiming = Mathf.Lerp(Aiming, wishAim ? 1 : 0, Time.deltaTime * aimSpeed);

        if(prevItem != _item)
        {
            Aiming = 0;
            if(reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }
            Reloading = 0;
        }
        prevItem = _item;

        if(wishReload && _item.Ammo < _item.data.ammoCap)
        {
            reloadCoroutine = StartCoroutine(Reload(_item));
            return;
        }

        if(wishAttack) Attack( _item, _state.Grounded);

        if(wishAim) _item.RightClick();
    }

    void Attack(ItemClient _item, bool grounded)
    {
        ItemData _data = _item.data;

        if(nextTimeToFire > Time.time) return;

        player.CallItemAction(false);

        if (_data.type is ItemType.Melee)
        {
            nextTimeToFire = Time.time + 1f / _data.fireRate;

            StartCoroutine(DelayShoot(_item, _data.attackDelay));

            SoundManager.instance.PlayNetworkSound(_data.AttackSound, cam.position);
        }

        if (_data.type is ItemType.Gun or ItemType.Shotgun or ItemType.Sniper)
        {
            if(!PlayerManager.instance.damageEnabled.Value) return;

            if(_item.Ammo <= 0) {
                if(PlayerManager.instance.reloadEnabled.Value) reloadCoroutine = StartCoroutine(Reload(_item));
                return;
            }

            _item.Ammo--;

            nextTimeToFire = Time.time + 1f / _data.fireRate;

            SoundManager.instance.PlayNetworkSound(_data.AttackSound, _item.muzzleTrans.position);

            Vector3 _recoil = new Vector3(-_data.Recoil.x, _data.Recoil.y * (Random.value < 0.5f ? -1.0f : 1.0f), _data.Recoil.z * (Random.value < 0.5f ? -1.0f : 1.0f)) * Mathf.Lerp(1f, _data.ADSRecoilMult, Aiming);
            float _backKick = -_data.backKick * Mathf.Lerp(1f, _data.ADSAnimMult, Aiming);
            float _rotKick = -_data.rotKick * Mathf.Lerp(1f, _data.ADSAnimMult, Aiming);

            shotPellets.Clear();
            if(_data.type is ItemType.Shotgun) {
                for (int i = 0; i < _data.numberOfShots; i++)
                    Shoot(_item);
            } else {
                Shoot(_item);
            }

            ShotFx fx = BuildShotFx(_item, _recoil, _backKick, _rotKick, true, !_data.useProjectile);
            PlayShotFx(fx);
            SendShotServerRpc(fx);

            if(_data.backwardVelocity != 0 && !grounded) {
                character.AddForce(-cam.forward * _data.backwardVelocity);
            }
        }
    }
    
    void Shoot(ItemClient _item)
    {
        ItemData _data = _item.data;
        
        Vector3 shootDir = cam.forward;
        if(_data.type != ItemType.Melee)
        {
            float curretAccuracy = Mathf.Lerp(_data.accuracy, _data.ADSAccuracy, Aiming);
            shootDir = cam.forward + new Vector3(Random.insideUnitSphere.x * curretAccuracy,  Random.insideUnitSphere.y * curretAccuracy, Random.insideUnitSphere.z * curretAccuracy);

            if(shootDir.sqrMagnitude > 0.0001f) shootDir.Normalize();
            else shootDir = cam.forward;
        }

        Vector3 spawnPos = _data.type != ItemType.Melee ? _item.muzzleTrans.position : cam.position;

        if(_data.useProjectile)
        {
            StartCoroutine(FireProjectile(
                spawnPos,
                shootDir,
                _data.projectileSize,
                _data.projectileSpeed,
                _data.projectileGravity,
                _data.projectileHitDamage,
                _data.projectileExplosionRadius,
                _data.projectileExplosionDamage,
                _data.projectileExplosionSelfDamage,
                _data.projectileLifetime,
                _data.impactForcePlayer,
                _data.impactForceObject,
                _data.projectileHitSound));
            VFXManager.instance.ProjectileFX(
                _data.ProjectileIndex,
                spawnPos,
                shootDir,
                _data.projectileSpeed,
                _data.projectileGravity,
                _data.projectileSize,
                _data.projectileLifetime);
            VFXManager.instance.MuzzleFlashFX(spawnPos);
            return;
        }
        
        int hitCount = _data.shootRadius > 0
            ? Physics.SphereCastNonAlloc(cam.position, _data.shootRadius, shootDir, shootHitsBuffer, _data.range, shootLayer)
            : Physics.RaycastNonAlloc(cam.position, shootDir, shootHitsBuffer, _data.range, shootLayer);

        if(hitCount == 0)
        {
            if(_data.type != ItemType.Melee)
            {
                Vector3 targetPoint = cam.transform.position + shootDir*_data.range;
                shotPellets.Add(new ShotPellet { end = targetPoint, normal = Vector3.zero, hit = false, trail = true, decal = 0 });
            } 
            
        } else
        {
            RaycastHit hitObject = shootHitsBuffer[0];
            for(int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = shootHitsBuffer[i];
                if((hit.distance < hitObject.distance && hit.transform.root != transform) || hitObject.transform.root == transform) { //shitty logic  <- pick closest point question mark ?
                    hitObject = hit;
                }
            }

            if(hitObject.transform.root == transform) //wtf
            {
                if(_data.type != ItemType.Melee)
                {
                    Vector3 targetPoint = cam.transform.position + shootDir*_data.range;
                    shotPellets.Add(new ShotPellet { end = targetPoint, normal = Vector3.zero, hit = false, trail = true, decal = 0 });
                } 
                return;
            }

            //Actualy hit something

            Transform hitRoot = hitObject.transform.root;

            int decalIndex = _data.DecalIndex;

            if (hitRoot.GetComponent<Player>()) //player damage
            {
                float _damage = hitObject.transform.tag == "Head" ? _data.damage * 2 : _data.damage;

                Vector3 _force = _data.impactForcePlayer == 0 ? Vector3.zero : shootDir * _data.impactForcePlayer + Vector3.up * upForceMult;
                Vector3 _propForce = shootDir * _data.impactForceObject * 0.4f * (_data.type is ItemType.Shotgun ? _data.numberOfShots * 0.5f : 1f);
                
                if (_data.type is ItemType.Melee) {
                    _force += character.State.Velocity;
                    _propForce += character.State.Velocity;
                } 

                PlayerManager.instance.DealDamageServerRpc(hitRoot.GetComponent<NetworkObject>().OwnerClientId, _damage, _force, _propForce);

                SoundManager.instance.PlaySound(hitSound, cam.position, true);

                decalIndex = playerHitDecalIndex;

                _item.OnHit(hitRoot.GetComponent<NetworkObject>().OwnerClientId);
            }
            else if(hitRoot.TryGetComponent(out ItemCrate crate))
            {
                crate.BreakCrateServerRpc();
                decalIndex = crateHitDecalIndex;
            }
            else if(hitRoot.TryGetComponent(out NetworkProp prop))
            {
                Vector3 propImpulse = shootDir * _data.impactForceObject;
                if (_data.type is ItemType.Melee) propImpulse += character.State.Velocity;
                prop.ApplyForceServerRpc(propImpulse, hitObject.point);
                decalIndex = playerHitDecalIndex; //kinda temp
            }

            shotPellets.Add(new ShotPellet {
                end = hitObject.point,
                normal = hitObject.normal,
                hit = true,
                trail = _data.type != ItemType.Melee,
                decal = decalIndex
            });
        }
    }

    IEnumerator FireProjectile(
        Vector3 origin,
        Vector3 direction,
        float size,
        float speed,
        float gravity,
        float onHitDamage,
        float explosionRadius,
        float explosionDamage,
        float explosionSelfDamage,
        float lifetime,
        float impactForcePlayer,
        float impactForceObject,
        SoundData projectileHitSound)
    {
        Vector3 position = origin;
        Vector3 velocity = direction * speed;
        float age = 0f;

        while (age < lifetime)
        {
            yield return new WaitForFixedUpdate();

            float dt = Time.fixedDeltaTime;
            age += dt;
            velocity += Vector3.down * gravity * dt;

            Vector3 displacement = velocity * dt;
            float distance = displacement.magnitude;
            if (distance <= 0f) continue;

            Vector3 stepDir = displacement / distance;
            int hitCount = Physics.SphereCastNonAlloc(position, size, stepDir, shootHitsBuffer, distance, shootLayer);

            RaycastHit? bestHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = shootHitsBuffer[i];
                if (hit.transform.root == transform.root) continue;
                if (bestHit == null || hit.distance < bestHit.Value.distance)
                    bestHit = hit;
            }

            if (bestHit.HasValue)
            {
                RaycastHit hit = bestHit.Value;
                position = hit.point;

                Transform hitRoot = hit.transform.root;
                if (hitRoot.GetComponent<Player>() != null && hitRoot.TryGetComponent(out NetworkObject netObj))
                {
                    PlayerManager.instance.DealDamageServerRpc(netObj.OwnerClientId, onHitDamage, Vector3.zero, Vector3.zero);

                    SoundManager.instance.PlaySound(hitSound, cam.position, true);
                }
                else if (hitRoot.TryGetComponent(out ItemCrate hitCrate))
                {
                    hitCrate.BreakCrateServerRpc();
                }

                if (explosionRadius > 0)
                {
                    ExplosionDamage(position, explosionRadius, explosionDamage, explosionSelfDamage, impactForcePlayer, impactForceObject);
                }

                SoundManager.instance.PlayNetworkSound(projectileHitSound, position);
                yield break;
            }

            position += displacement;
        }
    }

    void ExplosionDamage(Vector3 center, float explosionRadius, float explosionDamage, float explosionSelfDamage, float impactForcePlayer, float impactForceObject)
    {
        VFXManager.instance.PlayExplosion(center);

        explosionHitNetIds.Clear();

        int count = Physics.OverlapSphereNonAlloc(center, explosionRadius, explosionOverlapBuffer, shootLayer);
        for (int i = 0; i < count; i++)
        {
            Collider col = explosionOverlapBuffer[i];
            Transform root = col.transform.root;
            if (!root.TryGetComponent(out NetworkObject netObj)) continue;
            if (explosionHitNetIds.Contains(netObj.NetworkObjectId)) continue;

            Vector3 hitPoint = col.ClosestPoint(center);
            Vector3 toHit = hitPoint - center;
            float dist = toHit.magnitude;

            //los check
            if (Physics.Raycast(center, toHit / dist, out RaycastHit losHit, dist, shootLayer) && losHit.transform.root != root)
                continue;

            float falloff = 1f - Mathf.Pow(Mathf.Clamp01(dist / explosionRadius), 4);
            if (falloff <= 0.0001f) continue;

            explosionHitNetIds.Add(netObj.NetworkObjectId);

            Vector3 blastDir = toHit / dist;

            if (root.GetComponent<Player>() != null)
            {
                bool isSelf = root == transform.root;
                float damage = (isSelf ? explosionSelfDamage : explosionDamage) * falloff;
                
                Vector3 playerForce = impactForcePlayer == 0f ? Vector3.zero : blastDir * impactForcePlayer * falloff;
                Vector3 ragdollForce = impactForceObject == 0f ? Vector3.zero : blastDir * impactForceObject * falloff;

                PlayerManager.instance.DealDamageServerRpc(netObj.OwnerClientId, damage, playerForce, ragdollForce);

                SoundManager.instance.PlaySound(hitSound, cam.position, true);
            }
            else if (root.TryGetComponent(out ItemCrate crate))
            {
                crate.BreakCrateServerRpc();
            }
            else if (root.TryGetComponent(out NetworkProp prop) && impactForceObject != 0f)
            {
                prop.ApplyForceServerRpc(blastDir * (impactForceObject * falloff), hitPoint);
            }
        }
    }

    IEnumerator DelayShoot(ItemClient _item, float _delay)
    {
        yield return new WaitForSeconds(_delay);
        shotPellets.Clear();
        Shoot(_item);
        ShotFx fx = BuildShotFx(_item, Vector3.zero, 0f, 0f, false, false);
        PlayShotFx(fx);
        SendShotServerRpc(fx);
    }

    ShotFx BuildShotFx(ItemClient item, Vector3 recoil, float backKick, float rotKick, bool playRecoil, bool doMuzzle)
    {
        ItemData data = item.data;
        Vector3 muzzle = data.type != ItemType.Melee ? item.muzzleTrans.position : cam.position;
        return new ShotFx {
            muzzle = muzzle,
            recoil = recoil,
            backKick = backKick,
            rotKick = rotKick,
            playRecoil = playRecoil,
            doMuzzle = doMuzzle,
            pellets = shotPellets.ToArray()
        };
    }

    void PlayShotFx(ShotFx fx)
    {
        if (fx.playRecoil)
            animations.Shoot(fx.recoil, fx.backKick, fx.rotKick);

        if (fx.pellets == null) return;

        for (int i = 0; i < fx.pellets.Length; i++)
        {
            ShotPellet pellet = fx.pellets[i];
            if (fx.doMuzzle && i == 0) VFXManager.instance.PlayMuzzleFlash(fx.muzzle);
            if (pellet.trail) VFXManager.instance.PlayTrail(fx.muzzle, pellet.end);
            if (pellet.hit) VFXManager.instance.PlayDecal(pellet.end, pellet.normal, pellet.decal);
        }
    }

    [Rpc(SendTo.Server)]
    void SendShotServerRpc(ShotFx fx, RpcParams rpcParams = default)
    {
        SendShotClientRpc(fx, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void SendShotClientRpc(ShotFx fx, RpcParams rpcParams = default)
    {
        PlayShotFx(fx);
    }


    IEnumerator Reload(ItemClient _item) {
        float _reloadTime = _item.data.reloadSpeed;
        Reloading = 0;
        
        while(Reloading < 1) {
            Reloading += 1/_reloadTime * Time.deltaTime;
            yield return null;
        }

        _item.Ammo = _item.data.ammoCap;
        Reloading = 0;
        reloadCoroutine = null;
    }
}
