using UnityEngine;

[System.Serializable]
public struct HandData
{
    public Transform transform;
    public Vector3 startPos;
    public Quaternion startRot;
}

public abstract class ItemClient : MonoBehaviour
{
    [Header("Pickup")]
    public int slot;
    public bool cantDrop;
    public float pullOutTime;
    public int ammoCap;
    public int ammoSpawn;
    public bool isAutomatic;
    public bool canAttackWhileSprinting;

    [Header("View")]
    public Vector3 holdPosition;
    public bool rightHandIK;
    public bool leftHandIK;
    public bool idleIsMelee;
    public float adsZoom;
    public bool useScopeOverlay;
    public float recoilSnap;
    public float recoilReturnSpeed;
    public Vector3 ammoPos;
    public Vector3 sightPos;
    public Vector3 adsAmmoPos;
    public Vector3 widTopBot;

    public GameObject model;
    public Transform LHand, RHand;
    public Transform sight, muzzleTrans;

    public int Ammo;

    public float Aiming { get; protected set; }
    public float Reloading { get; protected set; }

    public GameObject PrefabAsset { get; set; }

    public bool ShowsHudAmmo => ammoCap > 0;

    protected CombatInputs inputs;

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

    protected bool FireHeldThisFrame => isAutomatic ? inputs.FireHeld : inputs.FirePressed;

    protected bool SprintBlocked(PlayerState state)
    {
        return state.Stance is Stance.Sprint && !canAttackWhileSprinting;
    }
}
