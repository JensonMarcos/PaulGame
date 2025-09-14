using System.Collections;
using UnityEngine;
// // using UnityEngine.Animations.Rigging;
// // using DG.Tweening;

public class GunAnimation : MonoBehaviour
{
    Rigidbody rb;
    Shooting player;
    MovementController movement;
    [SerializeField] ItemData currentItemData;

    Quaternion currentRot;
    
    [Header("Weapon Sway")]
    [SerializeField] float smooth = 7;
    [SerializeField] float swayMultiplayer = 1;
    Quaternion swayRot;
    Vector3 swayOffset;

    [Header("Weapon Bob")]
    [SerializeField] float freq = 16;
    [SerializeField] float sprintFreq = 22;
    [SerializeField] float mag = 0.4f;
    [SerializeField] float sprintMag = 1f;
    [SerializeField] float bobReturn = 3f;
    Vector3 bobOffset;
    
    [Header("Gun ADS Pos")]
    public float readyScopeThreshold = 0.0134f, adsTime;
    float adsMult, adsDeltaTime;
    public Vector3 ADSgunOffset, targetOffset, ogOffset;

    [Header("sprinting")]
    //public float sprintCoolDown;
    [SerializeField] float sprintEngageTime;
    [SerializeField] float sprintDisengageTime;
    public float shootSprintTime;
    //public float shootSprintThres;
    public bool sprinting;
    //public bool ScuffedsprintingGun;
    public Vector3 sprintOff, sprintRot;
    Vector3 targetSprintOff, targetSprintRot;

    [Header("pullout")]
    [SerializeField] float pulloutTime;
    float pullTimebackup;
    Vector3 pullOffset, pullRot;
    float pullAnimTime;

    [Header("Recoil")]
    Vector3 recoilOffset = Vector3.zero;
    Vector3 recoilRot = Vector3.zero;

    [Header("reloading")]
    public bool reloading;
    Quaternion reloadRot;

    [Header("lean")]
    [SerializeField] float leanAmmount = 4f;
    [SerializeField] float leanSpeed = 6f;
    Vector3 leanRot, targetLeanRot;

    void Start() {
        player = transform.root.GetComponent<Shooting>();
        movement = transform.root.GetComponent<MovementController>();
        ogOffset = new Vector3(0.15f, -0.17f, 0.3f);
        ADSgunOffset = ogOffset;
        reloading = false;
        reloadRot = Quaternion.Euler(Vector3.zero);
        rb = transform.root.GetComponent<Rigidbody>();
    }

    private void Update() {
        if(player.item == null && currentItemData == null) {
            return;
        } 
        if(player.item != null && currentItemData != player.item.data) {
            currentItemData = player.item.data.Clone();
        }

        adsMult = player.isScoping ? currentItemData.adsAnimMult : 1f;
        if((new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).sqrMagnitude > 10) && !movement.crouching && !player.isScoping){
            sprinting = Input.GetButton("Sprint");
        } else sprinting = false;

        if(sprinting && shootSprintTime <= Time.time) {
            targetSprintOff = Vector3.Lerp(targetSprintOff, sprintOff, sprintEngageTime * Time.deltaTime);
            targetSprintRot = Vector3.Slerp(targetSprintRot, sprintRot, sprintEngageTime * Time.deltaTime);
        } else {
            targetSprintOff = Vector3.Lerp(targetSprintOff, Vector3.zero, sprintDisengageTime * Time.deltaTime);
            targetSprintRot = Vector3.Slerp(targetSprintRot, Vector3.zero, sprintDisengageTime * Time.deltaTime);
        }

        // //new Vector3(currentItemData.adsOff.x, -currentItemData.sightPos.y, currentItemData.adsOff.z)
        targetOffset = player.isScoping ? currentItemData.adsOff : ogOffset;
        ADSgunOffset = Vector3.Lerp(ADSgunOffset, targetOffset, currentItemData.adsSpeed * 10f * Time.deltaTime);

        // // adsDeltaTime =  Mathf.Clamp(adsDeltaTime + Time.deltaTime * (player.isScoping ? 1f : -1f), 0f, currentItemData.adsSpeed);
        // // adsTime = Mathf.Clamp(adsDeltaTime / currentItemData.adsSpeed, 0f, 1f);
        // // //targetOffset = player.isScoping ? currentItemData.adsOff : ogOffset;
        // // ADSgunOffset = Vector3.Lerp(ogOffset, currentItemData.adsOff, adsTime);
        // //DOTween.To(()=> ADSgunOffset, x=> ADSgunOffset = x, targetOffset, currentItemData.adsSpeed).SetEase(Ease.OutElastic);
        

        player.readyScope = Vector3.Distance(ADSgunOffset, currentItemData.adsOff) < readyScopeThreshold;
        // //player.readyScope = adsTime >= 1;
        
        DoBob();

        

        if(Input.GetKeyDown("r") && player.item.ammo != currentItemData.ammoCap && !reloading && !sprinting) {
            reloading = true;
            StartCoroutine(reload());
        }

        DoSway();        
        
        HandlePull();

        recoilOffset = Vector3.Slerp(recoilOffset, Vector3.zero, currentItemData.animationReturn * Time.deltaTime);
        recoilRot = Vector3.Slerp(recoilRot, Vector3.zero, currentItemData.animationReturn * Time.deltaTime);
        
        targetLeanRot =  new Vector3(0, 0, -leanAmmount * Input.GetAxisRaw("Horizontal") * (player.isScoping ? 0f : 1f));
        leanRot = Vector3.Lerp(leanRot, targetLeanRot, leanSpeed * Time.deltaTime);

        //adds rotation for jumping, slide
        Quaternion targetRotation = Quaternion.Euler(movement.velocity.y * 0.8f * adsMult, 0, (Input.GetButton("Crouch") ? 22.5f : 0f)*(player.isScoping ? 0f : 1f));
        //player.camRecoil.targetRot.y * adsMult
        // Quaternion.Euler(player.camRecoil.targetRot.x * 0.4f * (player.isScoping ? 0.1f : 1f) * (isPistol ? 0 : 1), 0f, 0f);


        currentRot = Quaternion.Slerp(currentRot, targetRotation, smooth * Time.deltaTime);
        //pistolOff = Quaternion.Slerp(pistolOff, Quaternion.Euler(player.camRecoil.targetRot.x * targetPistolOff * (isPistol ? 1 : 0) * adsMult, 0f, 0f), pistolSnap * Time.deltaTime);

        transform.localRotation = swayRot * currentRot  * reloadRot * Quaternion.Euler(recoilRot + leanRot + pullRot + targetSprintRot);
        transform.localPosition = ADSgunOffset + bobOffset + recoilOffset + pullOffset + targetSprintOff + new Vector3(0, movement.velocity.y * -0.0008f * adsMult, 0);
    }

    private void DoBob() {
        //not moving
        if(new Vector3(movement.velocity.x, 0f, movement.velocity.z).sqrMagnitude < 0.1) {
            if(bobOffset != Vector3.zero) bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, bobReturn* 2 * Time.deltaTime);
            return;
        }
        Vector3 pos = Vector3.zero;
        float _mag = (sprinting ? sprintMag : mag) * adsMult;
        float _freq = sprinting ? sprintFreq : freq;
        pos.y += -Mathf.Sin(Time.time * _freq) * _mag * 0.1f;
        pos.x += Mathf.Cos(Time.time * _freq/2) * _mag * 0.1f;
        bobOffset += pos * (player.isScoping ? 0.1f : 1f) * Time.deltaTime;
        bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, bobReturn * Time.deltaTime);
    }

    void DoSway() {
        float mouseX = Input.GetAxisRaw("Mouse X") * swayMultiplayer * adsMult;
        float mouseY = Input.GetAxisRaw("Mouse Y") * swayMultiplayer * adsMult;

        Quaternion rotationX = Quaternion.AngleAxis(mouseY, Vector3.left);
        Quaternion rotationY = Quaternion.AngleAxis(mouseX, Vector3.up);

        Quaternion targetRot = rotationX * rotationY;
        swayRot = Quaternion.Slerp(swayRot, targetRot, smooth * Time.deltaTime);
    }

    void HandlePull() {
        pulloutTime = currentItemData.pullOutSpeed;

        pullAnimTime += Time.deltaTime;
        float s = Mathf.Clamp(pullAnimTime / pulloutTime, 0f, 1f);
        pullOffset = Vector3.Lerp(new Vector3(0, -0.5f, -0.5f), Vector3.zero, easeOutCirc(s));
        pullRot = Vector3.Lerp(new Vector3(80f, 0f, 0f), Vector3.zero, easeOutCirc(s));

        
        if(!player.readyPull) pullTimebackup += Time.deltaTime;
        if(pullTimebackup > pulloutTime) player.readyPull = true;
    }

    public void switchAnim() {
        StopAllCoroutines();
        reloading = false;
        reloadRot = Quaternion.Euler(Vector3.zero);
        //transform.localPosition += new Vector3(0, -0.5f, -0.5f);
        //pullOffset += new Vector3(0, -0.5f, -0.5f);
        pullAnimTime = 0;
        if(player.item != null) pulloutTime = currentItemData.pullOutSpeed;

        StartCoroutine(changePullout());
        pullTimebackup = 0;
    }
    
    IEnumerator changePullout() {
        yield return new WaitForSeconds(pulloutTime);
        player.readyPull = true;
    }

    float easeOutCirc(float x) {
        //return Mathf.Sqrt(1 - Mathf.Pow(x - 1, 2));
        return x == 1 ? 1 : 1 - Mathf.Pow(2, -10 * x);
    }

    float easeInCirc(float x) {
        return 1 - Mathf.Sqrt(1 - Mathf.Pow(x, 2));
    }

    public IEnumerator reload() {
        float spin = 0;
        reloadRot = Quaternion.Euler(Vector3.zero);
        
        //transform.localPosition = ogOffset;
        //ADSgunOffset = ogOffset;
        while(spin < 360) {
            if(player.item == null) {
                reloading = false;
                reloadRot = Quaternion.Euler(Vector3.zero);
                yield break;
            }
            spin += 360/currentItemData.reloadSpeed * Time.deltaTime;
            // reloadRot = Quaternion.Euler(-spin, 0, 0);
            reloadRot *= Quaternion.Euler(360/currentItemData.reloadSpeed * Time.deltaTime, 0, 0);
            yield return null;
        }
        player.item.ammo = currentItemData.ammoCap;
        reloading = false;
        reloadRot = Quaternion.Euler(Vector3.zero);
    }


    public void DoRecoil(float zKick, float xRotKick, float ranKick) {
        recoilOffset += new Vector3(0, 0, zKick) * adsMult;
        recoilRot += new Vector3(xRotKick * adsMult, 0, 0) + Random.Range(ranKick, -ranKick) * Vector3.one * adsMult;
    }

}
