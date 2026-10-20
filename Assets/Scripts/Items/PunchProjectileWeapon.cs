using UnityEngine;

public class PunchProjectileWeapon : PunchWeapon
{
    [Header("Projectile")]
    [SerializeField, Range(0f, 1f)] float minChargeForProjectile = 1f;
    public int projectileIndex;
    public float projectileSize;
    public float projectileSpeed;
    public float projectileGravity;
    public float projectileLifetime;

    [Space]
    public float projectileHitDamage;
    public float projectileCritDamage;
    public string projectileHitSound;

    [Space]
    public float projectileExplosionRadius;
    public float projectileExplosionDamage;
    public float projectileExplosionSelfDamage;

    PlayerCombat combat;
    bool fireProjectile;

    public override void Tick(PlayerCombat _combat, PlayerState state)
    {
        combat = _combat;
        base.Tick(_combat, state);
    }

    public override void PlayAttack(bool uppercut = false)
    {
        // Only the local owner has a combat reference and a charge value
        fireProjectile = combat != null && LastAttackCharge >= minChargeForProjectile;
        base.PlayAttack(uppercut);
    }

    protected override void OnPunchPeak(Transform hand)
    {
        if (!fireProjectile) return;
        fireProjectile = false;

        Vector3 spawnPos = hand.position;
        Vector3 dir = combat.Cam.forward;

        combat.StartCoroutine(ProjectileWeapon.SimProjectile(
            combat,
            spawnPos,
            dir,
            projectileSize,
            projectileSpeed,
            projectileGravity,
            projectileHitDamage,
            projectileCritDamage,
            projectileExplosionRadius,
            projectileExplosionDamage,
            projectileExplosionSelfDamage,
            projectileLifetime,
            impactForcePlayer,
            impactForceObject,
            projectileHitSound));

        VFXManager.instance.ProjectileFX(
            projectileIndex,
            spawnPos,
            dir,
            projectileSpeed,
            projectileGravity,
            projectileSize,
            projectileLifetime);
    }
}
