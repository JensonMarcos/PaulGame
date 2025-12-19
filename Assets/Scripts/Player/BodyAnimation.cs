using System.Collections;
using DitzelGames.FastIK;
using UnityEngine;

[System.Serializable]
public struct LookRig
{
    public Transform bone;
    public float weight, weightWhileAiming;
    public bool x, y, z;
}

public class BodyAnimation : MonoBehaviour
{
    public Animator BodyAnimator;

    [Header("Legs")]
    public float Stance;
    public float Sprint;
    public float Slide;
    public float Horizontal;
    public float Vertical;
    public float IdleState;

    [SerializeField] float moveResponse;
    [SerializeField] float crouchResponse;


    [Header("Rig")]
    [SerializeField] Transform cam;
    [SerializeField] LookRig[] aimRig;

    [SerializeField] Transform upperBodyBone;
    public float UpperBodyTilt;


    public void Initialize()
    {
        //
    }

    void Update()
    {
        BodyAnimator.SetFloat("Stance", Mathf.Lerp(BodyAnimator.GetFloat("Stance"), Stance, crouchResponse * Time.deltaTime));
        BodyAnimator.SetFloat("Sprint", Mathf.Lerp(BodyAnimator.GetFloat("Sprint"), Sprint, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat("Slide", Mathf.Lerp(BodyAnimator.GetFloat("Slide"), Slide, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat("Horizontal", Mathf.Lerp(BodyAnimator.GetFloat("Horizontal"), Horizontal, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat("Vertical", Mathf.Lerp(BodyAnimator.GetFloat("Vertical"), Vertical, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat("IdleState", Mathf.Lerp(BodyAnimator.GetFloat("IdleState"), IdleState, 10 * Time.deltaTime));
    }

    public void UpdateRigs(float headAim)
    {   
        upperBodyBone.transform.rotation *= Quaternion.Euler(0, UpperBodyTilt, 0);

        foreach (var rig in aimRig)
        {
            var weight = Mathf.Lerp(rig.weight, rig.weightWhileAiming, headAim);

            var newRot = Quaternion.Lerp(rig.bone.transform.rotation, cam.rotation, weight).eulerAngles;
            var oldRot = rig.bone.transform.eulerAngles;

            rig.bone.transform.rotation = Quaternion.Euler(rig.x ? newRot.x : oldRot.x, rig.y ? newRot.y : oldRot.y, rig.z ? newRot.z : oldRot.z);
        }

        //RHand.IK.Weight = LHand.IK.Weight = HandIKWeight;

        
    }

    public void SetAnimator(float _stance, float _sprint, float _slide, float x, float y, float idle)
    {
        Stance = _stance;
        Sprint = _sprint;
        Slide = _slide;
        Horizontal = x;
        Vertical = y;
        IdleState = idle;
    }

    public void TriggerAnimator(string name)
    {
        BodyAnimator.SetTrigger(name);
    }
}
