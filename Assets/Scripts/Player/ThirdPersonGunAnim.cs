using Unity.Netcode;
using UnityEngine;

public class ThirdPersonGunAnim : NetworkBehaviour
{
    public Transform gunHolder;
    public GunAnimation gunAnim;

    Vector3 targetPos, targetRot;

    void FixedUpdate()
    {
        if (!IsOwner) return;

        transform.position = gunHolder.position;
        transform.rotation = gunHolder.rotation;

        transform.localPosition += gunAnim.transform.localPosition - gunAnim.ogOffset;
        transform.localEulerAngles += gunAnim.transform.localEulerAngles;
    }
}
