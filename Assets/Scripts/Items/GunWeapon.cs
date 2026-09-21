using System.Collections;
using UnityEngine;

public abstract class GunWeapon : ItemClient
{
    [Header("Gun")]
    public float fireRate;
    public bool isAutomatic;
    public float reloadSpeed;
    public int ammoCap;
    public int ammoSpawn;

    [Space]
    public float accuracy;
    public float adsAccuracy;
    public float aimLerpSpeed = 20f;
    public float adsZoom;
    public bool useScopeOverlay;
    public bool cancelAdsWhileCycling;

    [Space]
    public Vector3 recoil;
    public float adsRecoilMult;
    public float recoilSnap;
    public float recoilReturnSpeed;
    public float backKick;
    public float rotKick;
    public float adsAnimMult;

    [Space]
    public float impactForceObject;
    public float impactForcePlayer;
    public float backwardVelocity;
    public string attackSound;
    public int decalIndex;

    [Space]
    [Header("View")]
    public bool idleIsMelee;
    public Transform sight;
    public Transform muzzleTrans;
    public Vector3 ammoPos;
    public Vector3 sightPos;
    public Vector3 adsAmmoPos;
    public Vector3 widTopBot;

    public override bool IdleIsMelee => idleIsMelee;
    public override float AdsZoom => adsZoom;
    public override bool UseScopeOverlay => useScopeOverlay;
    public override float RecoilSnap => recoilSnap;
    public override float RecoilReturnSpeed => recoilReturnSpeed;
    public override int AmmoCap => ammoCap;
    public override int AmmoSpawn => ammoSpawn;
    public override Vector3 AmmoPos => ammoPos;
    public override Vector3 SightPos => sightPos;
    public override Vector3 AdsAmmoPos => adsAmmoPos;
    public override Vector3 WidTopBot => widTopBot;
    public override Transform Sight => sight;

    float nextTimeToFire;
    Coroutine reloadCoroutine;

    public override void OnUnequip()
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            reloadCoroutine = null;
        }
        base.OnUnequip();
    }

    public override void Tick(PlayerCombat combat, PlayerState state)
    {
        bool reloading = Reloading > 0f;
        bool wantAim = inputs.AltHeld && !reloading && !SprintBlocked(state);
        if (cancelAdsWhileCycling && nextTimeToFire > Time.time) wantAim = false;
        Aiming = Mathf.Lerp(Aiming, wantAim ? 1f : 0f, Time.deltaTime * aimLerpSpeed);

        if (reloading) return;

        if (inputs.ReloadPressed && Ammo < ammoCap && ReloadEnabled())
        {
            reloadCoroutine = StartCoroutine(Reload());
            return;
        }

        if (!FireHeldThisFrame || SprintBlocked(state)) return;
        if (nextTimeToFire > Time.time) return;

        if (!DamageEnabled()) return;

        if (Ammo <= 0)
        {
            if (ReloadEnabled()) reloadCoroutine = StartCoroutine(Reload());
            return;
        }

        Fire(combat, state);
    }

    void Fire(PlayerCombat combat, PlayerState state)
    {
        ConsumeShot();
        SoundManager.Play(attackSound, muzzleTrans.position);
        Shoot(combat);

        if (backwardVelocity != 0f && !state.Grounded)
            combat.Character.AddForce(-combat.Cam.forward * backwardVelocity);
    }

    protected abstract void Shoot(PlayerCombat combat);

    bool FireHeldThisFrame => isAutomatic ? inputs.FireHeld : inputs.FirePressed;

    void ConsumeShot()
    {
        Ammo--;
        nextTimeToFire = Time.time + 1f / fireRate;
    }

    protected Vector3 SpreadDirection(PlayerCombat combat)
    {
        float spread = Mathf.Lerp(accuracy, adsAccuracy, Aiming);
        Vector3 dir = combat.Cam.forward + new Vector3(
            Random.insideUnitSphere.x * spread,
            Random.insideUnitSphere.y * spread,
            Random.insideUnitSphere.z * spread);
        if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
        return combat.Cam.forward;
    }

    protected ShotFx BuildRecoilFx(Vector3 muzzle, ShotPellet[] pellets, bool doMuzzle)
    {
        Vector3 kick = new Vector3(
            -recoil.x,
            recoil.y * (Random.value < 0.5f ? -1f : 1f),
            recoil.z * (Random.value < 0.5f ? -1f : 1f)) * Mathf.Lerp(1f, adsRecoilMult, Aiming);

        return new ShotFx {
            muzzle = muzzle,
            recoil = kick,
            backKick = -backKick * Mathf.Lerp(1f, adsAnimMult, Aiming),
            rotKick = -rotKick * Mathf.Lerp(1f, adsAnimMult, Aiming),
            playRecoil = true,
            doMuzzle = doMuzzle,
            pellets = pellets
        };
    }

    IEnumerator Reload()
    {
        Reloading = 0f;
        float speed = Mathf.Max(reloadSpeed, 0.01f);
        while (Reloading < 1f)
        {
            Reloading += Time.deltaTime / speed;
            yield return null;
        }

        Ammo = ammoCap;
        Reloading = 0f;
        reloadCoroutine = null;
    }

    static bool DamageEnabled()
    {
        return PlayerManager.instance == null || PlayerManager.instance.damageEnabled.Value;
    }

    static bool ReloadEnabled()
    {
        return PlayerManager.instance == null || PlayerManager.instance.reloadEnabled.Value;
    }
}
