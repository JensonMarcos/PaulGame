using UnityEngine;

public class HitscanWeapon : GunWeapon
{
    [Header("Hitscan")]
    public float damage;
    public float critDamage;

    [Space]
    public float range;
    public float shootRadius;
    public int numberOfShots;

    protected override void Shoot(PlayerCombat combat)
    {
        int pellets = numberOfShots > 0 ? numberOfShots : 1;
        float ragdollMult = numberOfShots > 1 ? numberOfShots * 0.5f : 1f;
        ShotPellet[] hits = new ShotPellet[pellets];

        for (int i = 0; i < pellets; i++)
        {
            Vector3 shootDir = SpreadDirection(combat);
            if (!combat.TryTrace(combat.Cam.position, shootDir, shootRadius, range, out RaycastHit hit))
            {
                hits[i] = new ShotPellet {
                    end = combat.Cam.position + shootDir * range,
                    normal = Vector3.zero,
                    hit = false,
                    trail = true,
                    decal = 0
                };
                continue;
            }

            float hitDamage = IsHeadshot(hit) ? critDamage : damage;
            hits[i] = combat.HitscanHit(hit, shootDir, hitDamage, impactForcePlayer, impactForceObject, ragdollMult, decalIndex).pellet;
        }

        combat.PlayShotFx(BuildRecoilFx(muzzleTrans.position, hits, true));
    }

    static bool IsHeadshot(RaycastHit hit)
    {
        return hit.transform.CompareTag("Head") && hit.transform.root.GetComponent<Player>() != null;
    }
}
