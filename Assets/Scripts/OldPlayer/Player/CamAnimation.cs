using UnityEngine;

public class CamAnimation : MonoBehaviour
{
    //public MovementController movement;
    //public GunAnimation gunAnim;
    public Vector3 currentRot, targetRot, leanRot;//, targetLeanRot;
    //private Shooting gun;
    public float snap = 6f, returnSpeed = 2f;//, leanAmmount, leanSpeed;

    // [Header("bob")]
    // public float freq;
    // public float mag;
    // public float bobReturn;
    // Vector3 bobOffset;

    void Update() {
        //targetLeanRot =  new Vector3(0, 0, -leanAmmount * Input.GetAxisRaw("Horizontal"));
        //leanRot = Vector3.Lerp(leanRot, targetLeanRot, leanSpeed * Time.deltaTime);

        targetRot = Vector3.Lerp(targetRot, Vector3.zero, returnSpeed * Time.deltaTime);
        currentRot = Vector3.Slerp(currentRot, targetRot, snap * Time.fixedDeltaTime);

        //DoBob();

        transform.localRotation = Quaternion.Euler(currentRot);
        //transform.localPosition = bobOffset;

        
    }

    public void RecoilFire(float x, float y, float z, float adsMult) {
        targetRot += new Vector3(x, y, z) * adsMult;
    }

    // private void DoBob() {
    //     //not moving
    //     // if(new Vector3(movement.velocity.x, 0f, movement.velocity.z).sqrMagnitude < 0.1) {
    //     //     if(bobOffset != Vector3.zero) bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, bobReturn* 2 * Time.deltaTime);
    //     //     return;
    //     // }
    //     if(gunAnim.ScuffedsprintingGun) {
    //         Vector3 pos = Vector3.zero;
    //         pos.y += -Mathf.Sin((Time.time + Mathf.PI) * freq) * mag;
    //         pos.x += Mathf.Cos((Time.time + Mathf.PI) * freq/2) * mag;
    //         bobOffset += pos * Time.deltaTime;
    //     }

    //     if(bobOffset != Vector3.zero) {
    //         bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, bobReturn * Time.deltaTime);
    //     }
    // }
}
