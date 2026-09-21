using UnityEngine;

[System.Serializable]
public struct HandData
{
    public Transform transform;
    [HideInInspector] public Vector3 startPos;
    [HideInInspector] public Quaternion startRot;
}

public abstract class ItemClient : MonoBehaviour
{
    [Header("Item")]
    public int slot;
    public bool cantDrop;
    public float pullOutTime;
    public bool canAttackWhileSprinting;

    [Space]
    [Header("View")]
    public Vector3 holdPosition;
    public bool rightHandIK;
    public bool leftHandIK;
    public GameObject model;
    public Transform LHand;
    public Transform RHand;

    [HideInInspector] public int Ammo;

    public virtual bool IdleIsMelee => false;
    public virtual float AdsZoom => 0f;
    public virtual bool UseScopeOverlay => false;
    public virtual float RecoilSnap => 0f;
    public virtual float RecoilReturnSpeed => 0f;
    public virtual int AmmoCap => 0;
    public virtual int AmmoSpawn => 0;
    public virtual Vector3 AmmoPos => default;
    public virtual Vector3 SightPos => default;
    public virtual Vector3 AdsAmmoPos => default;
    public virtual Vector3 WidTopBot => default;
    public virtual Transform Sight => null;

    public bool ShowsHudAmmo => AmmoCap > 0;

    protected CombatInputs inputs;

    public float Aiming { get; protected set; }
    public float Reloading { get; protected set; }

    public GameObject PrefabAsset { get; set; }

    public virtual void OnUnequip()
    {
        Aiming = 0f;
        Reloading = 0f;
    }

    public virtual void SetInputs(CombatInputs combatInputs)
    {
        inputs = combatInputs;
    }

    public virtual void Tick(PlayerCombat combat, PlayerState state) { }

    public virtual void PlayAttack() { }

    public virtual void PlayCharge() { }

    public virtual void StopCharge() { }

    protected bool SprintBlocked(PlayerState state)
    {
        return state.Stance is Stance.Sprint && !canAttackWhileSprinting;
    }
}
