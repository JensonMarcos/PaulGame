using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MeleeWeapon : ItemClient
{
    [Header("Melee")]
    public float fireRate;
    public float damage;
    public float range;
    public float shootRadius;
    public float meleeHitDuration = 0.12f;
    public float impactForceObject;
    public float impactForcePlayer;
    public string attackSound;
    public int decalIndex;

    float nextTimeToFire;
    Coroutine sweepCoroutine;
    readonly HashSet<Transform> hitRoots = new HashSet<Transform>();
    readonly List<RaycastHit> traces = new List<RaycastHit>(16);
    readonly List<ShotPellet> pellets = new List<ShotPellet>(8);

    void Awake()
    {
        idleIsMelee = true;
    }

    public override void OnUnequip()
    {
        StopAllCoroutines();
        base.OnUnequip();
    }

    public override void Tick(PlayerCombat combat, PlayerState state)
    {
        Aiming = 0f;
        if (!FireHeldThisFrame || SprintBlocked(state)) return;
        if (nextTimeToFire > Time.time) return;

        nextTimeToFire = Time.time + 1f / fireRate;

        PlayAttack();
        combat.ReplicateAttack();

        if (sweepCoroutine != null) StopCoroutine(sweepCoroutine);
        sweepCoroutine = StartCoroutine(Sweep(combat));

        SoundManager.Play(attackSound, combat.Cam.position);
    }

    IEnumerator Sweep(PlayerCombat combat)
    {
        hitRoots.Clear();
        float endTime = Time.time + meleeHitDuration;

        while (true)
        {
            Cast(combat);
            if (Time.time >= endTime) break;
            yield return null;
        }

        sweepCoroutine = null;
    }

    void Cast(PlayerCombat combat)
    {
        combat.TraceAll(combat.Cam.position, combat.Cam.forward, shootRadius, range, traces);
        pellets.Clear();

        for (int i = 0; i < traces.Count; i++)
        {
            RaycastHit hit = traces[i];
            Transform hitRoot = hit.transform.root;
            if (!hitRoots.Add(hitRoot)) continue;

            CombatHit result = combat.MeleeHit(
                hit,
                combat.Cam.forward,
                damage,
                impactForcePlayer,
                impactForceObject,
                decalIndex);

            pellets.Add(result.pellet);

            if (result.isPlayer)
                OnPlayerHit(result.playerId);
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
