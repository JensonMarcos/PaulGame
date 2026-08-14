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

    static readonly int StanceHash = Animator.StringToHash("Stance");
    static readonly int SprintHash = Animator.StringToHash("Sprint");
    static readonly int SlideHash = Animator.StringToHash("Slide");
    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");
    static readonly int IdleStateHash = Animator.StringToHash("IdleState");

    public void Initialize()
    {
        //
    }

    void Update()
    {
        BodyAnimator.SetFloat(StanceHash, Mathf.Lerp(BodyAnimator.GetFloat(StanceHash), Stance, crouchResponse * Time.deltaTime));
        BodyAnimator.SetFloat(SprintHash, Mathf.Lerp(BodyAnimator.GetFloat(SprintHash), Sprint, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat(SlideHash, Mathf.Lerp(BodyAnimator.GetFloat(SlideHash), Slide, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat(HorizontalHash, Mathf.Lerp(BodyAnimator.GetFloat(HorizontalHash), Horizontal, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat(VerticalHash, Mathf.Lerp(BodyAnimator.GetFloat(VerticalHash), Vertical, moveResponse * Time.deltaTime));
        BodyAnimator.SetFloat(IdleStateHash, Mathf.Lerp(BodyAnimator.GetFloat(IdleStateHash), IdleState, 10 * Time.deltaTime));
    }

    float weight;
    Vector3 newRot;
    
    public void UpdateRigs(float headAim, float overallWeight)
    {   
        upperBodyBone.transform.rotation *= Quaternion.Euler(0, UpperBodyTilt, 0);

        foreach (var rig in aimRig)
        {
            weight = Mathf.Lerp(0, Mathf.Lerp(rig.weight, rig.weightWhileAiming, headAim), overallWeight);
            newRot = Quaternion.Lerp(rig.bone.transform.rotation, cam.rotation, weight).eulerAngles;

            rig.bone.transform.rotation = Quaternion.Euler(rig.x ? newRot.x : rig.bone.transform.eulerAngles.x, rig.y ? newRot.y : rig.bone.transform.eulerAngles.y, rig.z ? newRot.z : rig.bone.transform.eulerAngles.z);
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

    public void SetLayerWeight(int layer, float weight)
    {
        BodyAnimator.SetLayerWeight(layer, weight);
    }

    public void TriggerAnimator(string name)
    {
        BodyAnimator.SetTrigger(name);
    }
}
