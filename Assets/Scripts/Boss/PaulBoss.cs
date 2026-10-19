using UnityEngine;

public class PaulBoss : Boss
{
    [SerializeField] PunchWeapon punch;

    public override ItemClient Melee => punch;
    protected override ItemClient RightItem => punch;

    //gun on left click without ADS, punch on right click
    protected override CombatInputs ItemInputs(CombatInputs _inputs) => new CombatInputs {
        FirePressed = _inputs.FirePressed,
        FireHeld = _inputs.FireHeld,
        FireReleased = _inputs.FireReleased,
        ReloadPressed = _inputs.ReloadPressed
    };

    public override void Enter()
    {
        base.Enter();
        if(Equipped is GunWeapon gun)
        {
            gun.ignoreReloadRule = true;
            gun.Ammo = gun.AmmoCap;
        }
    }

    public override void UpdateCombat(PlayerState _state)
    {
        base.UpdateCombat(_state);

        punch.SetInputs(new CombatInputs {
            FirePressed = inputs.AltPressed,
            FireHeld = inputs.AltHeld,
            FireReleased = inputs.AltReleased
        });
        punch.Tick(combat, _state);
    }
}
