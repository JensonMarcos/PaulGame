using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float Aiming;

    [SerializeField] PlayerInventory inventory;
    [SerializeField] PlayerAnimations animations;

    [SerializeField] float aimSpeed;

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

    public void UpdateCombat(PlayerState _state, ItemData _data)
    {
        if(wishAttack)
        {
            Attack(_data);
        }
    }

    void Attack(ItemData _data)
    {
        if (_data.type is ItemType.Melee)
        {
            animations.Attack();
            animations.AttackServerRpc();
        }
        else if (_data.type is ItemType.Gun or ItemType.Shotgun or ItemType.Sniper)
        {
            Vector3 _recoil = new Vector3(-_data.recoilX, _data.recoilY * (Random.value < 0.5f ? -1.0f : 1.0f), _data.recoilZ * (Random.value < 0.5f ? -1.0f : 1.0f));
            float _backKick = -_data.backKick;
            animations.Shoot(_recoil, _backKick);
            animations.ShootServerRpc(_recoil, _backKick);
        }
    }
    
    void Shoot()
    {
        
    }
}
