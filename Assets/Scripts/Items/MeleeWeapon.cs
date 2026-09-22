using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MeleeWeapon : ItemClient
{
    [Header("Melee")]
    public float fireRate;
    public float onHitCooldownReduction;
    public float chargeDuration = 0.4f;
    public float meleeHitDuration = 0.12f;
    public float range;
    public float chargeRange;
    public float shootRadius;

    [Space]
    public float damage;
    public float critDamage;
    public float impactForceObject;
    public float impactForcePlayer;
    public float uppercutForce;
    public float chargeKnockback;
    public float chargeVelocity;

    [Space]
    public string attackSound;
    public int decalIndex;

    public override bool IdleIsMelee => true;

    float nextTimeToFire;
    bool hitCooldownReduced;
    float charge;
    bool charging;
    Coroutine sweepCoroutine;
    protected Coroutine attackRoutine;
    protected Coroutine chargeRoutine;
    readonly HashSet<Transform> hitRoots = new HashSet<Transform>();
    readonly List<RaycastHit> traces = new List<RaycastHit>(16);
    readonly List<ShotPellet> pellets = new List<ShotPellet>(8);

    protected float ChargeTime => Mathf.Max(chargeDuration, 0.01f);

    protected void StopAttackRoutine()
    {
        if (attackRoutine == null) return;
        StopCoroutine(attackRoutine);
        attackRoutine = null;
    }

    public override void OnUnequip()
    {
        charging = false;
        charge = 0f;
        StopAllCoroutines();
        attackRoutine = null;
        chargeRoutine = null;
        sweepCoroutine = null;
        StopCharge();
        base.OnUnequip();
    }

    public override void Tick(PlayerCombat combat, PlayerState state)
    {
        Aiming = 0f;

        if (SprintBlocked(state) || state.Stance is Stance.Vault)
        {
            CancelCharge(combat);
            return;
        }

        if (!charging)
        {
            if (nextTimeToFire > Time.time) return;
            if (!inputs.FireHeld && !inputs.FirePressed) return;

            charging = true;
            charge = 0f;
            //StopAttackRoutine();
            PlayCharge();
            combat.ReplicateCharge(true);
        }

        charge = Mathf.Min(1f, charge + Time.deltaTime / ChargeTime);

        if (inputs.FireReleased)
            Attack(combat, state);
        else if (!inputs.FireHeld)
            CancelCharge(combat);
    }

    void CancelCharge(PlayerCombat combat)
    {
        if (!charging) return;
        charging = false;
        charge = 0f;
        StopCharge();
        combat.ReplicateCharge(false);
    }

    void Attack(PlayerCombat combat, PlayerState state)
    {
        charging = false;
        float attackCharge = charge;
        charge = 0f;
        nextTimeToFire = Time.time + 1f / fireRate;
        bool uppercut = state.Velocity.y > 0.1f;

        float velocityScale = Mathf.InverseLerp(0.5f, 1f, attackCharge);
        if (chargeVelocity != 0f && velocityScale > 0f)
        {
            Vector3 direction = combat.Cam.forward;
            float along = Vector3.Dot(combat.Character.Motor.BaseVelocity, direction);
            float add = Mathf.Min(chargeVelocity * velocityScale, Mathf.Max(0f, chargeVelocity * 2f - along));
            if (add > 0f)
                combat.Character.AddForce(direction * add);
        }

        //StopAttackRoutine();
        PlayAttack(uppercut);
        combat.ReplicateAttack(uppercut);

        if (sweepCoroutine != null) StopCoroutine(sweepCoroutine);
        sweepCoroutine = StartCoroutine(Sweep(combat, attackCharge, uppercut));

        SoundManager.Play(attackSound, combat.Cam.position);
    }

    IEnumerator Sweep(PlayerCombat combat, float attackCharge, bool uppercut)
    {
        hitRoots.Clear();
        hitCooldownReduced = false;
        float endTime = Time.time + meleeHitDuration;

        while (true)
        {
            Cast(combat, attackCharge, uppercut);
            if (Time.time >= endTime) break;
            yield return null;
        }

        sweepCoroutine = null;
    }

    void Cast(PlayerCombat combat, float attackCharge, bool uppercut)
    {
        float hitRange = Mathf.Lerp(range, chargeRange, attackCharge);
        combat.TraceAll(combat.Cam.position, combat.Cam.forward, shootRadius, hitRange, traces);
        pellets.Clear();
        Vector3 hitDir = uppercut ? (Vector3.up + 0.3f*combat.Cam.forward).normalized : combat.Cam.forward;

        for (int i = 0; i < traces.Count; i++)
        {
            RaycastHit hit = traces[i];
            Transform hitRoot = hit.transform.root;
            if (!hitRoots.Add(hitRoot)) continue;

            CombatHit result = combat.MeleeHit(
                hit,
                hitDir,
                Mathf.Lerp(damage, critDamage, attackCharge),
                uppercut ? uppercutForce : Mathf.Lerp(impactForcePlayer, chargeKnockback, attackCharge),
                impactForceObject * (uppercut ? 0.5f : 1f),
                decalIndex);

            pellets.Add(result.pellet);

            if (result.isPlayer)
            {
                OnPlayerHit(result.playerId);
                if (!hitCooldownReduced && onHitCooldownReduction > 0f)
                {
                    hitCooldownReduced = true;
                    nextTimeToFire -= onHitCooldownReduction;
                }
            }
        }

        if (pellets.Count == 0) return;

        combat.PlayShotFx(new ShotFx {
            muzzle = combat.Cam.position,
            recoil = Vector3.zero,
            backKick = 0f,
            rotKick = 0f,
            playRecoil = false,
            doMuzzle = false,
            pellets = pellets.ToArray()
        });
    }

    protected virtual void OnPlayerHit(ulong targetId) { }
}
