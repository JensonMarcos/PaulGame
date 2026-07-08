using System.Collections;
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

    readonly RaycastHit[] shootHitsBuffer = new RaycastHit[16];

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
            StopAllCoroutines();
            Reloading = 0;
        }
        prevItem = _item;

        if(wishReload && _item.Ammo < _item.data.ammoCap)
        {
            StartCoroutine(Reload(_item));
            return;
        }

        if(wishAttack) Attack( _item, _state.Grounded);

        if(wishAim) _item.RightClick();
    }

    void Attack(ItemClient _item, bool grounded)
    {
        ItemData _data = _item.data;

        if(nextTimeToFire > Time.time) return;

        _item.LeftClick();

        

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
                if(PlayerManager.instance.reloadEnabled) StartCoroutine(Reload(_item));
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
        
        
        int hitCount = _data.shootRadius > 0
            ? Physics.SphereCastNonAlloc(cam.position, _data.shootRadius, cam.forward + accuracyOffset, shootHitsBuffer, _data.range, shootLayer)
            : Physics.RaycastNonAlloc(cam.position, cam.forward + accuracyOffset, shootHitsBuffer, _data.range, shootLayer);

        if(hitCount == 0)
        {
            //shoot Fx in air
            if(_data.type != ItemType.Melee)
            {
                Vector3 targetPoint = cam.transform.position + cam.transform.forward*_data.range;
                FXManager.instance.ShootFX(_item.muzzleTrans.position, targetPoint, Vector3.zero, false, true, firstShot, 0);
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
                    Vector3 targetPoint = cam.transform.position + cam.transform.forward*_data.range;
                    FXManager.instance.ShootFX(_item.muzzleTrans.position, targetPoint, Vector3.zero, false, true, firstShot, 0);
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
                PlayerManager.instance.DealDamageServerRpc(hitRoot.GetComponent<NetworkObject>().OwnerClientId, _damage, _force);
                
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


            if(_data.type != ItemType.Melee) FXManager.instance.ShootFX(_item.muzzleTrans.position, hitObject.point, Vector3.zero, true, true, firstShot, 0);
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
    }
}
