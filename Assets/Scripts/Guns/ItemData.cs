using UnityEngine;

[CreateAssetMenu(fileName = "ItemData", menuName = "Scriptable Objects/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("Type (0=primary, 1=secondary, 2=melee)")]
    public int type;
    public bool sniper, shotgun;

    [Header("Basic Stats")]
    public float damage;
    public float fireRate, reloadSpeed, pullOutSpeed;
    public int ammoCap;
    public bool semiAuto;
    public float range = 100f;

    [Header("Accuracy")]
    public float accuracy;
    public float ADSaccuracy;
    public float SprintAccuracy;

    [Header("Recoil")]
    public float recoilX;
    public float recoilY;
    public float recoilZ;
    public float adsRecoilMult;
    public float snap, returnSpeed;

    [Header("ADS")]
    public float adsSpeed;
    public float adsZoom;
    public Vector3 adsOff;

    [Header("Animation")] 
    public float backKick;
    public float upKick;
    public float randomKick;
    public float animationReturn;
    public float adsAnimMult;

    [Header("ShotGun")] 
    public bool isShotGun;
    public int numberOfShots;

    [Header("GunHUD")]
    public Vector3 AmmoPos;
    public Vector3 sightPos, ADSAmmoPos, WidTopBot;

    [Header("Physics")]
    public float bulletForce;
    public float backwardVelocity;

    public ItemData Clone()
    {
        return (ItemData)this.MemberwiseClone();
    }

}
