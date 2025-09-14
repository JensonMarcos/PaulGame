using UnityEngine;
using Unity.Netcode;

public class Shooting : NetworkBehaviour
{
    PlayerManager playerManager;
    Inventory inventory;
    MovementController movement;
    [SerializeField] Camera cam;
    //[SerializeField] Transform gunTransform;
    [SerializeField] LayerMask shootLayer;
    [SerializeField] CamAnimation camAnim;
    [SerializeField] Aiming aim;
    public GunAnimation gunAnim;

    public GameObject curretItem;
    public Item item;
    public GhostItem clientItem;

    float nextTimeToFire;
    Vector3 accuracyOffset;
    float curretAccuracy;
    public bool isScoping, readyScope, readyPull;
    [SerializeField] float fov = 63, targetFov, gunFov, sprintFov = 8, slideFov = 12, fovReturnSpeed = 4;

    void Start() {
        if(!IsOwner) return;
        inventory = GetComponent<Inventory>();
        playerManager = PlayerManager.instance;
        movement = GetComponent<MovementController>();
    }

    void Update() {
        if(!IsOwner) return;

        if(curretItem != inventory.inventory[inventory.activeGun]) {
            if(inventory.inventory[inventory.activeGun] == null) {
                curretItem = null;
                item = null;
                clientItem = null;
            } else {
                curretItem = inventory.inventory[inventory.activeGun];
                item = curretItem.GetComponent<Item>();
                clientItem = inventory.clientInventory[inventory.activeGun].GetComponent<GhostItem>();
            }
        }   
        if(curretItem != null) {
            isScoping = Input.GetButton("Fire2");
            if(gunAnim.reloading) isScoping = false;
            if(item.data.sniper && camAnim.targetRot.x < -0.3f) isScoping = false;


            if ((item.data.semiAuto ? Input.GetButtonDown("Fire1") : Input.GetButton("Fire1")) && Time.time >= nextTimeToFire && !gunAnim.reloading && readyPull)  
            {
                if(gunAnim.sprinting) gunAnim.shootSprintTime = Time.time + 0.5f;
                //print("shoot");
                Attack();
            } 
        } else {
            isScoping = false;
        }
        targetFov = Mathf.Lerp(targetFov, (movement.sprinting ? sprintFov : 0) + (movement.sliding ? slideFov : 0), fovReturnSpeed * Time.deltaTime);

        gunFov = Mathf.Lerp(gunFov, item == null ? fov : (isScoping ? item.data.adsZoom : fov), (item == null ? 30 : item.data.adsSpeed * 10f) * Time.deltaTime);

        cam.fieldOfView = gunFov + targetFov;
        aim.ADSsensitivity = cam.fieldOfView/fov;
    }

    void Attack() {
        if(item.ammo<=0 && !gunAnim.reloading && item.data.type != 2) {
            gunAnim.reloading = true;
            gunAnim.StartCoroutine(gunAnim.reload());
            return;
        }

        item.ammo--;

        camAnim.snap = item.data.snap;
        camAnim.returnSpeed = item.data.returnSpeed;
        Vector3 recoil = new Vector3(
            item.data.recoilX,
            Random.Range(item.data.recoilY, -item.data.recoilY), 
            Random.Range(item.data.recoilZ, -item.data.recoilZ));
        camAnim.RecoilFire(recoil.x, recoil.y, recoil.z, (isScoping && readyScope) ? item.data.adsRecoilMult : 1);

        gunAnim.DoRecoil(item.data.backKick, item.data.upKick, item.data.randomKick);

        if(item.data.backwardVelocity != 0) movement.velocity += -cam.transform.forward * item.data.backwardVelocity;

        //recoilOffset.y += item.recoilGun;

        // item.gunScShoot();
        // gunClass.GetComponent<PhotonView>().RPC("Playdataund", RpcTarget.All, activeGun);
        //gunAnim.ShootGun();
        nextTimeToFire = Time.time + 1f / item.data.fireRate;
        
        if(item.data.shotgun) {
            for (int i = 0; i < item.data.numberOfShots; i++)
            {
                Shoot();
            }
        } else {
            Shoot();
        }
        
    }

    void Shoot() {
        curretAccuracy = (!gunAnim.sprinting) ? ((isScoping && readyScope) ? item.data.ADSaccuracy : item.data.accuracy) : item.data.SprintAccuracy;
        accuracyOffset = new Vector3(Random.insideUnitSphere.x * curretAccuracy,  Random.insideUnitSphere.y * curretAccuracy, Random.insideUnitSphere.z * curretAccuracy);
        

        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward + accuracyOffset, out hit, item.data.range, shootLayer.value) && hit.transform.root.transform != this.transform) {
            //print(hit.transform.name);

            if (hit.transform.root.GetComponent<Health>()) //player damage
            {
                float _damage = hit.transform.tag == "Head" ? item.data.damage * 2 : item.data.damage;

                playerManager.DealDamageServerRpc(hit.transform.root.GetComponent<NetworkObject>().OwnerClientId, _damage);
                
                //hit indicator shit
                // hitSound.pitch = Random.Range(0.95f, 1.05f);
                // hitSound.PlayOneShot(hitSound.clip, 1f);
                // HUD.HUDHit(hit.transform.tag == "Head");
            }

            if (hit.rigidbody != null) //rb force
            {
                hit.rigidbody.AddForce(cam.transform.forward * item.data.bulletForce, ForceMode.Impulse);
            }

            GameFX.instance.LocalShootFX(clientItem.muzzleTrans.position, hit.point, hit.normal, true, true, 0);
        } else {
            GameFX.instance.LocalShootFX(clientItem.muzzleTrans.position, cam.transform.position + cam.transform.forward*100, Vector3.zero, false, true, 0);
        }
    }
}
