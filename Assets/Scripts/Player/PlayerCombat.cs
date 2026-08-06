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

public class PlayerCombat : MonoBehaviour
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
                if(PlayerManager.instance.reloadEnabled) reloadCoroutine = StartCoroutine(Reload(_item));
                return;
            }

            SoundManager.instance.PlayNetworkSound(_data.AttackSound, _item.muzzleTrans.position);

            _item.Ammo--;

            nextTimeToFire = Time.time + 1f / _data.fireRate;

            if(_data.type is ItemType.Shotgun) {
                for (int i = 0; i < _data.numberOfShots; i++)
                {
                    Shoot(_item, i == 0);
                }
            } else {
                Shoot(_item, true);
            }

            Vector3 _recoil = new Vector3(-_data.Recoil.x, _data.Recoil.y * (Random.value < 0.5f ? -1.0f : 1.0f), _data.Recoil.z * (Random.value < 0.5f ? -1.0f : 1.0f)) * Mathf.Lerp(1f, _data.ADSRecoilMult, Aiming);
            float _backKick = -_data.backKick * Mathf.Lerp(1f, _data.ADSAnimMult, Aiming);
            float _rotKick = -_data.rotKick * Mathf.Lerp(1f, _data.ADSAnimMult, Aiming);
            animations.Shoot(_recoil, _backKick, _rotKick);
            animations.ShootServerRpc(_recoil, _backKick, _rotKick);

            if(_data.backwardVelocity != 0 && !grounded) {
                character.AddForce(-cam.forward * _data.backwardVelocity);
            }
        }
    }
    
    void Shoot(ItemClient _item, bool firstShot)
    {
        ItemData _data = _item.data;
        
        Vector3 accuracyOffset = Vector3.zero;
        if(_data.type != ItemType.Melee)
        {
            float curretAccuracy = Mathf.Lerp(_data.accuracy, _data.ADSAccuracy, Aiming);
            accuracyOffset = new Vector3(Random.insideUnitSphere.x * curretAccuracy,  Random.insideUnitSphere.y * curretAccuracy, Random.insideUnitSphere.z * curretAccuracy);
        }

        if(_data.useProjectile)
        {
            Vector3 shootDir = (cam.forward + accuracyOffset).normalized;
            Vector3 spawnPos = _item.muzzleTrans.position;
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
            if(firstShot) VFXManager.instance.MuzzleFlashFX(spawnPos);
            return;
        }
        
        int hitCount = _data.shootRadius > 0
            ? Physics.SphereCastNonAlloc(cam.position, _data.shootRadius, cam.forward + accuracyOffset, shootHitsBuffer, _data.range, shootLayer)
            : Physics.RaycastNonAlloc(cam.position, cam.forward + accuracyOffset, shootHitsBuffer, _data.range, shootLayer);

        if(hitCount == 0)
        {
            //shoot Fx in air
            if(_data.type != ItemType.Melee)
            {
                Vector3 targetPoint = cam.transform.position + (cam.transform.forward+accuracyOffset)*_data.range;
                VFXManager.instance.ShootFX(_item.muzzleTrans.position, targetPoint, Vector3.zero, false, true, firstShot, 0);
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
                //shoot Fx in air
                if(_data.type != ItemType.Melee)
                {
                    Vector3 targetPoint = cam.transform.position + (cam.transform.forward+accuracyOffset)*_data.range;
                    VFXManager.instance.ShootFX(_item.muzzleTrans.position, targetPoint, Vector3.zero, false, true, firstShot, 0);
                } 
                return;
            }

            //Actualy hit something

            //print(hitObject.transform.name);
            Transform hitRoot = hitObject.transform.root;

            if (hitRoot.GetComponent<Player>()) //player damage
            {
                float _damage = hitObject.transform.tag == "Head" ? _data.damage * 2 : _data.damage;

                Vector3 _force = _data.impactForcePlayer == 0 ? Vector3.zero : cam.transform.forward * _data.impactForcePlayer + Vector3.up * upForceMult;
                Vector3 _propForce = cam.transform.forward * _data.impactForceObject * 0.4f * (_data.type is ItemType.Shotgun ? _data.numberOfShots * 0.5f : 1f);
                PlayerManager.instance.DealDamageServerRpc(hitRoot.GetComponent<NetworkObject>().OwnerClientId, _damage, _force, _propForce);
                
                //hit indicator shit
                // hitSound.pitch = Random.Range(0.95f, 1.05f);
                // hitSound.PlayOneShot(hitSound.clip, 1f);
                // HUD.HUDHit(hitObject.transform.tag == "Head");
            } 
            else if(hitRoot.TryGetComponent(out ItemCrate crate))
            {
                crate.BreakCrateServerRpc();
            }
            else if(hitRoot.TryGetComponent(out NetworkProp prop))
            {
                prop.ApplyForceServerRpc(cam.transform.forward * _data.impactForceObject, hitObject.point);
            }


            if(_data.type != ItemType.Melee) VFXManager.instance.ShootFX(_item.muzzleTrans.position, hitObject.point, hitObject.normal, true, true, firstShot, _data.DecalIndex);
            else VFXManager.instance.DecalFX(hitObject.point, hitObject.normal, _data.DecalIndex);
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
        VFXManager.instance.ExplosionFX(center);

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
        Shoot(_item, false);
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
