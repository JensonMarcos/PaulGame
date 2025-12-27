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
    public float pullOutSpeed;
    [ShowIf("isGun")] public float fireRate, reloadSpeed;
    [ShowIf("isGun")] public int ammoCap;
    [ShowIf("isGun")] public bool semiAuto;
    [ShowIf("isGun")] public float range = 100f;

    [Header("Accuracy")]
    [ShowIf("isGun")] public float accuracy;
    [ShowIf("isGun")] public float ADSaccuracy;
    [ShowIf("isGun")] public float SprintAccuracy;

    [Header("Recoil")]
    [ShowIf("isGun")] public float recoilX;
    [ShowIf("isGun")] public float recoilY;
    [ShowIf("isGun")] public float recoilZ;
    [ShowIf("isGun")] public float adsRecoilMult;
    [ShowIf("isGun")] public float snap, returnSpeed;

    [Header("ADS")]
    [ShowIf("isGun")] public float adsSpeed;
    [ShowIf("isGun")] public float adsZoom;
    [ShowIf("isGun")] public Vector3 adsOff;

    [Header("Animation")] 
    [ShowIf("isGun")] public float backKick;
    [ShowIf("isGun")] public float upKick;
    [ShowIf("isGun")] public float randomKick;
    [ShowIf("isGun")] public float animationReturn;
    [ShowIf("isGun")] public float adsAnimMult;

    [Header("ShotGun")] 
    [ShowIf("isShotgun")] public int numberOfShots;

    [Header("GunHUD")]
    [ShowIf("isGun")] public Vector3 AmmoPos, ADSAmmoPos;
    [ShowIf("isGun")] public Vector3 sightPos, WidTopBot;

    [Header("Physics")]
    [ShowIf("isGun")] public float bulletForce;
    [ShowIf("isGun")] public float backwardVelocity;

    public ItemData Clone()
    {
        return (ItemData)this.MemberwiseClone();
    }

}
