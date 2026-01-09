using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float Aiming;

    public float Reloading;

    //[SerializeField] PlayerInventory inventory;
    [SerializeField] PlayerAnimations animations;
    [SerializeField] Transform cam;

    [SerializeField] float aimSpeed;
    [SerializeField] LayerMask shootLayer;

    bool wishAttack;
    bool wishAim;
    bool wishReload;

    float nextTimeToFire;
    bool hasFiredSemi;

    ItemClient prevItem;

    public void SetInputs(PlayerInputs _inputs, bool _sprinting, bool _readyPull, bool _isAutomatic)
    {
        if(_inputs.Attack) { //semi auto and auto handling
            if(_isAutomatic) {
                wishAttack = true;
            }
            else {
                if(!hasFiredSemi) {
                    wishAttack = true;
                    hasFiredSemi = true;
                } else {
                    wishAttack = false;
                }
            }
        }
        else {
            wishAttack = false;
            hasFiredSemi = false;
        }

        wishAim = _inputs.Aim;
        wishReload = _inputs.Reload;

        if (_sprinting || !_readyPull || Reloading > 0) {
            wishAttack = false;
            wishAim = false;
            wishReload = false;
        }
        
        Aiming = Mathf.Lerp(Aiming, wishAim ? 1 : 0, Time.deltaTime * aimSpeed);

    }

    public void UpdateCombat(PlayerState _state, ItemClient _item)
    {
        if(prevItem != _item)
        {
            StopAllCoroutines();
            Reloading = 0;
        }
        prevItem = _item;

        if(wishReload && _item.Ammo < _item.data.ammoCap)
        {
            StartCoroutine(Reload(_item));
            return;
        }

        if(wishAttack)
        {
            Attack( _item);
        }

        if(wishAim)
        {
            _item.RightClick();
        }
    }

    void Attack(ItemClient _item)
    {
        ItemData _data = _item.data;

        _item.LeftClick();

        if (_data.type is ItemType.Melee)
        {
            // animations.Attack();
            // animations.AttackServerRpc();
        }
        else if (_data.type is ItemType.Gun or ItemType.Shotgun or ItemType.Sniper && nextTimeToFire <= Time.time)
        {
            if(_item.Ammo <= 0) {
                StartCoroutine(Reload(_item));
                return;
            }

            _item.Ammo--;

            Vector3 _recoil = new Vector3(-_data.Recoil.x, _data.Recoil.y * (Random.value < 0.5f ? -1.0f : 1.0f), _data.Recoil.z * (Random.value < 0.5f ? -1.0f : 1.0f)) * Mathf.Lerp(1f, _data.ADSRecoilMult, Aiming);
            float _backKick = -_data.backKick * Mathf.Lerp(1f, _data.ADSAnimMult, Aiming);
            animations.Shoot(_recoil, _backKick);
            animations.ShootServerRpc(_recoil, _backKick);

            nextTimeToFire = Time.time + 1f / _data.fireRate;

            if(_data.type is ItemType.Shotgun) {
                for (int i = 0; i < _data.numberOfShots; i++)
                {
                    Shoot(_item);
                }
            } else {
                Shoot(_item);
            }
        }
    }
    
    void Shoot(ItemClient _item)
    {
        ItemData _data = _item.data;

        float curretAccuracy = Mathf.Lerp(_data.accuracy, _data.ADSAccuracy, Aiming);
        Vector3 accuracyOffset = new Vector3(Random.insideUnitSphere.x * curretAccuracy,  Random.insideUnitSphere.y * curretAccuracy, Random.insideUnitSphere.z * curretAccuracy);


        Vector3 targetPoint = cam.transform.position + cam.transform.forward*100;
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward + accuracyOffset, out hit, _data.range, shootLayer.value) && hit.transform.root.transform != this.transform) {
            print(hit.transform.name);

            if (hit.transform.root.GetComponent<Player>()) //player damage
            {
                float _damage = hit.transform.tag == "Head" ? _data.damage * 2 : _data.damage;

                PlayerManager.instance.DealDamageServerRpc(hit.transform.root.GetComponent<NetworkObject>().OwnerClientId, _damage);
                
                //hit indicator shit
                // hitSound.pitch = Random.Range(0.95f, 1.05f);
                // hitSound.PlayOneShot(hitSound.clip, 1f);
                // HUD.HUDHit(hit.transform.tag == "Head");
            }

            // if (hit.rigidbody != null) //rb force
            // {
            //     hit.rigidbody.AddForce(cam.transform.forward * item.data.bulletForce, ForceMode.Impulse);
            // }
            targetPoint = hit.point;
        }
        GameFX.instance.LocalShootFX(_item.muzzleTrans.position, targetPoint, Vector3.zero, false, true, 0);
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
