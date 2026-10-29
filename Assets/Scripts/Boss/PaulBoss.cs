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

    Renderer[] shockwaveRenderers;
    MaterialPropertyBlock shockwaveProps;
    float blastTimer = -1f;
    float pullTimer;

    PaulBossMovement paulMovement => (PaulBossMovement)movement;

    public override ItemClient Melee => punch;
    protected override ItemClient RightItem => punch;

    void Awake()
    {
        shockwaveRenderers = shockwave.GetComponentsInChildren<Renderer>(true);
        shockwaveProps = new MaterialPropertyBlock();
        syncedSuperLandings.OnValueChanged += OnSuperLanding;
    }

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

        syncedCharge.Value = (byte)Mathf.RoundToInt(Mathf.Clamp01(paulMovement.JumpCharge) * 255f);
        syncedSuperLandings.Value = paulMovement.SuperLandings;
    }

    public override void UpdateAnimations(PlayerState _state, float _deltaTime)
    {
        base.UpdateAnimations(_state, _deltaTime);
        UpdateShockwave(_state, _deltaTime);
    }

    //late joiners get the current count without this firing, so they dont see an old blast
    void OnSuperLanding(byte _previous, byte _current)
    {
        blastTimer = 0f;
        if(IsOwner) Blast(); //only the owner deals the damage and push, the sound is networked
    }

    void UpdateShockwave(PlayerState _state, float _deltaTime)
    {
        //landing blast: expands rapidly, full opacity then fades out over the end
        if(blastTimer >= 0f)
        {
            blastTimer += _deltaTime;
            float _t = blastTimer / shockwaveBlastTime;
            if(_t < 1f)
            {
                float _ease = 1f - (1f - _t) * (1f - _t);
                SetShockwave(shockwaveBlastScale * _ease, Mathf.Clamp01((1f - _t) / shockwaveFadeFraction));
                return;
            }
            blastTimer = -1f;
        }

        //charging: only while crouched, starts big and shrinks to nothing, fully faded in by the fade fraction
        float _charge = syncedCharge.Value / 255f;
        if(_state.Stance == Stance.Crouch && _charge > 0f)
            SetShockwave(Mathf.Lerp(shockwaveStartScale, 0f, _charge), Mathf.Clamp01(_charge / shockwaveFadeFraction));
        else
            HideShockwave();
    }

    void SetShockwave(float _scale, float _opacity)
    {
        if(!shockwave.gameObject.activeSelf) shockwave.gameObject.SetActive(true);
        shockwave.localScale = Vector3.one * _scale;

        //fade every renderer under the shockwave, scaling each material's own alpha
        foreach(Renderer _renderer in shockwaveRenderers)
        {
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
        if(shockwave.gameObject.activeSelf) shockwave.gameObject.SetActive(false);
    }

    //the shockwave is 1 meter across at a scale of 1
    static float ShockwaveRadius(float _scale) => _scale * 0.5f;

    Vector3 EffectCenter => character.transform.position + Vector3.up * effectHeight;

    //push and damage everything around the boss, same as a normal explosion but without the fire effect and without hurting itself
    void Blast()
    {
        combat.Explosion(EffectCenter, ShockwaveRadius(shockwaveBlastScale), blastDamage, 0f, blastForcePlayer, blastForceObject, playFx: false, affectSelf: false);
        SoundManager.Play(blastSound, EffectCenter);
    }

    //while charging, suck in every player and prop inside the shockwave's starting size (the stronger the charge, the stronger the pull)
    void UpdatePull()
    {
        if(!paulMovement.Charging)
        {
            pullTimer = 0f;
            return;
        }

        pullTimer += Time.deltaTime;
        if(pullTimer < pullInterval) return;

        //forces are applied in bursts, so scale them to the time since the last burst
        float _scale = pullTimer * paulMovement.JumpCharge;
        pullTimer = 0f;
        combat.Pull(EffectCenter, ShockwaveRadius(shockwaveStartScale), pullForcePlayer * _scale, pullForceObject * _scale);
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
