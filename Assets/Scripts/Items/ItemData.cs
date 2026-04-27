using UnityEngine;
using NaughtyAttributes;

public enum ItemType
{
    Gun, Sniper, Shotgun, Melee
}

// public enum Slot { 
//     Primary, Secondary, Melee
// }

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    public ItemType type;
    public int slot;

    bool isGun => type is ItemType.Gun or ItemType.Sniper or ItemType.Shotgun;
    bool isSniper => type is ItemType.Sniper;
    bool isShotgun => type is ItemType.Shotgun;
    bool isMelee => type is ItemType.Melee;

    [Header("Basic Stats")]
    public float damage;
    public Vector3 position;
    public float pullOutTime;
    public float fireRate;
    [ShowIf("isMelee")] public float attackDelay;
    [ShowIf("isGun")] public float reloadSpeed;
    [ShowIf("isGun")] public int ammoCap;
    [ShowIf("isGun")] public int ammoSpawn;
    public bool isAutomatic;
    public float range;
    public float shootRadius;
    
    [Header("Accuracy")]
    [ShowIf("isGun")] public float accuracy;
    [ShowIf("isGun")] public float ADSAccuracy;
    [ShowIf("isGun")] public float SprintAccuracy;

    [Header("Recoil")]
    [ShowIf("isGun")] public Vector3 Recoil;
    [ShowIf("isGun")] public float ADSRecoilMult;
    [ShowIf("isGun")] public float snap, returnSpeed;

    [Header("ADS")]
    [ShowIf("isGun")] public float adsSpeed;
    [ShowIf("isGun")] public float adsZoom;
    //[ShowIf("isGun")] public Vector3 adsOff;

    [Header("Animation")] 
    public bool RightHandIK;
    public bool LeftHandIK;
    [ShowIf("isGun")] public float backKick;
    // [ShowIf("isGun")] public float upKick;
    // [ShowIf("isGun")] public float randomKick;
    // [ShowIf("isGun")] public float animationReturn;
    [ShowIf("isGun")] public float ADSAnimMult;

    [Header("ShotGun")] 
    [ShowIf("isShotgun")] public int numberOfShots;

    [Header("GunHUD")]
    [ShowIf("isGun")] public Vector3 AmmoPos;
    [ShowIf("isGun")] public Vector3 sightPos;
    [ShowIf("isGun")] public Vector3 ADSAmmoPos;
    [ShowIf("isGun")] public Vector3 WidTopBot;

    [Header("Physics")]
    public float impactForceObject;
    public float impactForcePlayer;
    [ShowIf("isGun")] public float backwardVelocity;

    public ItemData Clone()
    {
        return (ItemData)this.MemberwiseClone();
    }

}
