using System.Collections;
using UnityEngine;

public class ProjectileWeapon : GunWeapon
{
    [Header("Projectile")]
    public int projectileIndex;
    public float projectileSize;
    public float projectileSpeed;
    public float projectileGravity;
    public float projectileLifetime;

    [Space]
    public float projectileHitDamage;
    public float critDamage;
    public string projectileHitSound;

    [Space]
    public float projectileExplosionRadius;
    public float projectileExplosionDamage;
    public float projectileExplosionSelfDamage;

    protected override void Shoot(PlayerCombat combat)
    {
        Vector3 spawnPos = muzzleTrans.position;
        Vector3 shootDir = SpreadDirection(combat);

        combat.StartCoroutine(SimProjectile(
            combat,
            spawnPos,
            shootDir,
            projectileSize,
            projectileSpeed,
            projectileGravity,
            projectileHitDamage,
            critDamage,
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
            shootDir,
            projectileSpeed,
            projectileGravity,
            projectileSize,
            projectileLifetime);
        VFXManager.instance.MuzzleFlashFX(spawnPos);

        combat.PlayShotFx(BuildRecoilFx(spawnPos, null, false));
    }

    static IEnumerator SimProjectile(
        PlayerCombat combat,
        Vector3 origin,
        Vector3 direction,
        float size,
        float speed,
        float gravity,
        float hitDamage,
        float critDamage,
        float explosionRadius,
        float explosionDamage,
        float explosionSelfDamage,
        float lifetime,
        float forcePlayer,
        float forceObject,
        string hitSound)
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
            if (combat.TryTrace(position, stepDir, size, distance, out RaycastHit hit))
            {
                position = hit.point;
                Transform hitRoot = hit.transform.root;

                if (hitRoot.GetComponent<Player>() != null && hitRoot.TryGetComponent(out Unity.Netcode.NetworkObject netObj))
                {
                    float dmg = hit.transform.CompareTag("Head") ? critDamage : hitDamage;
                    PlayerManager.instance.DealDamageServerRpc(netObj.OwnerClientId, dmg, Vector3.zero, Vector3.zero);
                    SoundManager.Play("hitmarker");
                }
                else if (hitRoot.TryGetComponent(out ItemCrate hitCrate))
                {
                    hitCrate.BreakCrateServerRpc();
                }

                if (explosionRadius > 0f)
                {
                    combat.Explosion(
                        position,
                        explosionRadius,
                        explosionDamage,
                        explosionSelfDamage,
                        forcePlayer,
                        forceObject);
                }

                SoundManager.Play(hitSound, position);
                yield break;
            }

            position += displacement;
        }
    }
}
