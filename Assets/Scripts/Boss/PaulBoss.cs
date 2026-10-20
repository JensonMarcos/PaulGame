using Unity.Netcode;
using UnityEngine;

public class PaulBoss : Boss
{
    [SerializeField] PunchProjectileWeapon punch;

    [Header("Shockwave")]
    [SerializeField] Transform shockwave;
    [SerializeField] float shockwaveStartScale = 1.5f; //size when crouch starts, shrinks to 0 at full charge
    [SerializeField] float shockwaveBlastScale = 6f; //size it expands to on landing from a full super jump
    [SerializeField] float shockwaveBlastTime = 0.2f; //seconds for the landing expansion
    [SerializeField, Range(0.01f, 1f)] float shockwaveFadeFraction = 0.5f; //fraction of the charge/blast spent fading (0.5 = fully faded in by half charge, fades out over the second half of the blast)

    [Header("Pull (while charging)")]
    [SerializeField] float pullForcePlayer = 40f; //velocity per second added to players at full charge, point blank
    [SerializeField] float pullForceObject = 100f; //impulse per second added to props at full charge, point blank
    [SerializeField] float pullInterval = 0.1f; //seconds between pulls (each one is a network message per target)

    [Header("Blast (landing a full super jump)")]
    [SerializeField] float blastDamage = 30f;
    [SerializeField] float blastForcePlayer = 30f;
    [SerializeField] float blastForceObject = 80f;
    [SerializeField] string blastSound = "explosion";
    [SerializeField] float effectHeight = 0.5f; //how far above the feet the pull and blast are centered

    static readonly int[] ColorIds = { Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_Color"), Shader.PropertyToID("_TintColor") };

    //owner writes these, everyone reads them to drive the shockwave
    readonly NetworkVariable<byte> syncedCharge = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<byte> syncedSuperLandings = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    PaulBossMovement paulMovement;
    Renderer[] shockwaveRenderers;
    MaterialPropertyBlock shockwaveProps;
    float blastTimer = -1f;
    float pullTimer;
    byte lastSuperLandings;
    bool landingsInitialized;

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

    public override void Exit()
    {
        HideShockwave();
        base.Exit();
    }

    public override void SetDead(bool _dead)
    {
        base.SetDead(_dead);
        if(_dead) HideShockwave();
    }

    public override void WriteState(ref PlayerState _state)
    {
        base.WriteState(ref _state);

        if(!IsOwner || !IsSpawned) return;
        if(paulMovement == null) paulMovement = movement as PaulBossMovement;
        if(paulMovement == null) return;

        syncedCharge.Value = (byte)Mathf.RoundToInt(Mathf.Clamp01(paulMovement.JumpCharge) * 255f);
        syncedSuperLandings.Value = paulMovement.SuperLandings;
    }

    public override void UpdateAnimations(PlayerState _state, float _deltaTime)
    {
        base.UpdateAnimations(_state, _deltaTime);
        UpdateShockwave(_state, _deltaTime);
    }

    void UpdateShockwave(PlayerState _state, float _deltaTime)
    {
        if(shockwave == null) return;

        if(shockwaveRenderers == null)
        {
            shockwaveRenderers = shockwave.GetComponentsInChildren<Renderer>(true);
            shockwaveProps = new MaterialPropertyBlock();
        }

        //a full super jump just landed (first read just syncs up so joining late doesnt fire a blast)
        byte _landings = syncedSuperLandings.Value;
        if(landingsInitialized && _landings != lastSuperLandings)
        {
            blastTimer = 0f;
            if(IsOwner) Blast(); //only the owner deals the damage and push, the sound is networked
        }
        lastSuperLandings = _landings;
        landingsInitialized = true;

        float _charge = syncedCharge.Value / 255f;

        //landing blast: expands rapidly, full opacity then fades out over the end
        if(blastTimer >= 0f)
        {
            blastTimer += _deltaTime;
            float _t = Mathf.Clamp01(blastTimer / Mathf.Max(shockwaveBlastTime, 0.01f));
            if(_t < 1f)
            {
                float _ease = 1f - (1f - _t) * (1f - _t);
                float _blastOpacity = Mathf.Clamp01((1f - _t) / shockwaveFadeFraction);
                SetShockwave(Mathf.Lerp(0f, shockwaveBlastScale, _ease), _blastOpacity);
                return;
            }
            blastTimer = -1f;
        }

        //charging: only while crouched, starts big and shrinks to nothing, fully faded in by the fade fraction
        if(_state.Stance == Stance.Crouch && _charge > 0f)
        {
            float _chargeOpacity = Mathf.Clamp01(_charge / shockwaveFadeFraction);
            SetShockwave(Mathf.Lerp(shockwaveStartScale, 0f, _charge), _chargeOpacity);
        }
        else HideShockwave();
    }

    void SetShockwave(float _scale, float _opacity)
    {
        if(!shockwave.gameObject.activeSelf) shockwave.gameObject.SetActive(true);
        shockwave.localScale = Vector3.one * _scale;

        //fade every renderer under the shockwave, scaling each material's own alpha
        foreach(Renderer _renderer in shockwaveRenderers)
        {
            if(_renderer == null) continue;

            Material[] _mats = _renderer.sharedMaterials;
            for(int i = 0; i < _mats.Length; i++)
            {
                if(_mats[i] == null) continue;

                _renderer.GetPropertyBlock(shockwaveProps, i);
                foreach(int _id in ColorIds)
                {
                    if(!_mats[i].HasProperty(_id)) continue;
                    Color _color = _mats[i].GetColor(_id);
                    _color.a *= _opacity;
                    shockwaveProps.SetColor(_id, _color);
                }
                _renderer.SetPropertyBlock(shockwaveProps, i);
            }
        }
    }

    void HideShockwave()
    {
        blastTimer = -1f;
        if(shockwave != null && shockwave.gameObject.activeSelf) shockwave.gameObject.SetActive(false);
    }

    //the shockwave is 1 meter across at a scale of 1
    float ShockwaveRadius(float _scale) => shockwave == null ? 0f : _scale * 0.5f;

    //push and damage everything around the boss, same as a normal explosion but without the fire effect and without hurting itself
    //reaches as far as the shockwave's fully expanded size
    void Blast()
    {
        float _radius = ShockwaveRadius(shockwaveBlastScale);
        if(_radius <= 0f) return;

        Vector3 _center = character.transform.position + Vector3.up * effectHeight;
        combat.Explosion(_center, _radius, blastDamage, 0f, blastForcePlayer, blastForceObject, playFx: false, affectSelf: false);
        SoundManager.Play(blastSound, _center);
    }

    //while charging, suck in every player and prop inside the shockwave's starting size (the stronger the charge, the stronger the pull)
    void UpdatePull()
    {
        if(paulMovement == null) paulMovement = movement as PaulBossMovement;
        float _radius = ShockwaveRadius(shockwaveStartScale);
        if(paulMovement == null || _radius <= 0f || !paulMovement.Charging || paulMovement.JumpCharge <= 0f)
        {
            pullTimer = 0f;
            return;
        }

        pullTimer += Time.deltaTime;
        if(pullTimer < pullInterval) return;

        //forces are applied in bursts, so scale them to the time since the last burst
        float _scale = pullTimer * paulMovement.JumpCharge;
        pullTimer = 0f;
        combat.Pull(character.transform.position + Vector3.up * effectHeight, _radius, pullForcePlayer * _scale, pullForceObject * _scale);
    }

    public override void UpdateCombat(PlayerState _state)
    {
        base.UpdateCombat(_state);
        UpdatePull();

        punch.SetInputs(new CombatInputs {
            FirePressed = inputs.AltPressed,
            FireHeld = inputs.AltHeld,
            FireReleased = inputs.AltReleased
        });
        punch.Tick(combat, _state);
    }
}
