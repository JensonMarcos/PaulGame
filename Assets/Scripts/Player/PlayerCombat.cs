using Unity.Netcode;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float Aiming;

    //[SerializeField] PlayerInventory inventory;
    [SerializeField] PlayerAnimations animations;
    [SerializeField] Transform cam;

    [SerializeField] float aimSpeed;
    [SerializeField] LayerMask shootLayer;

    bool wishAttack;
    bool wishAim;

    public void SetInputs(PlayerInputs _inputs, bool _sprinting, bool _readyPull)
    {
        wishAttack = _inputs.Attack;
        wishAim = _inputs.Aim;

        if (_sprinting || !_readyPull) {
            wishAttack = false;
            wishAim = false;
        }
        
        Aiming = Mathf.Lerp(Aiming, wishAim ? 1 : 0, Time.deltaTime * aimSpeed);
    }

    public void UpdateCombat(PlayerState _state, ItemClient _item)
    {
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
        else if (_data.type is ItemType.Gun or ItemType.Shotgun or ItemType.Sniper)
        {
            Vector3 _recoil = new Vector3(-_data.recoilX, _data.recoilY * (Random.value < 0.5f ? -1.0f : 1.0f), _data.recoilZ * (Random.value < 0.5f ? -1.0f : 1.0f));
            float _backKick = -_data.backKick;
            animations.Shoot(_recoil, _backKick);
            animations.ShootServerRpc(_recoil, _backKick);

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

        float curretAccuracy = Mathf.Lerp(_data.accuracy, _data.ADSaccuracy, Aiming);
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
}
