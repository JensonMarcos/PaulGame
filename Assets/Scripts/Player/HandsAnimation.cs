using UnityEngine;
using System.Collections;
using DitzelGames.FastIK;

[System.Serializable]
public struct HandRig
{
    public Transform hand;
    public FastIKFabric IK;
    // public Vector3 startPos;
    // public Quaternion startRot;
}

public class HandsAnimation : MonoBehaviour
{
    [Header("Rig")]
    public HandRig RHand, LHand;
    [Range(0, 1)]
    public float HandIKWeight;
    [SerializeField] Transform HandParent;
    [SerializeField] float weightChangeSpeed;

    // [Header("Punch")]
    // [SerializeField] Transform punchTarget;
    // [SerializeField] float punchSpeed;
    // [SerializeField] float punchHoldTime;
    // [SerializeField] BodyAnimation body;
    // [SerializeField] float tiltAmount;
    //bool whichhand; 
    //bool readyPunch;

    [Header("Pullout Animation")]
    [SerializeField] Vector3 pulloutPosOffset;
    [SerializeField] Vector3 pulloutRotOffset;
    Vector3 pullRot;
    Vector3 pullPos;
    float pulloutTime, pullAnimTime;

    [Header("Reload Animation")]
    [SerializeField] float displacementAmount;
    [SerializeField] float rotationAmount;
    [SerializeField] float cosPow;

    Transform lastTargetTrans;
    float lastReloadingVal;

    // public void Initialize()
    // {
    //     // RHand.startPos = RHand.hand.localPosition;
    //     // RHand.startRot = RHand.hand.localRotation;
    //     // LHand.startPos = LHand.hand.localPosition;
    //     // LHand.startRot = LHand.hand.localRotation;

    //     //readyPunch = true;
    // }

    public void UpdateRigs(Transform _rHand, Transform _lHand, bool _sprinting)
    {
        float targetWeight = _sprinting ? 0f : 1f;
        HandIKWeight = Mathf.Lerp(HandIKWeight, targetWeight, weightChangeSpeed * Time.deltaTime);

        RHand.IK.Weight = LHand.IK.Weight = HandIKWeight; //change this to individual weights later

        if (_rHand != null)
        {
            RHand.hand.position = _rHand.position;
            RHand.hand.rotation = _rHand.rotation;
        }

        if (_lHand != null)
        {
            LHand.hand.position = _lHand.position;
            LHand.hand.rotation = _lHand.rotation;
        }


        RHand.IK.ResolveIK();
        LHand.IK.ResolveIK();

    }

    public void UpdateTransform(Transform _target, float _reloading)
    {
        lastTargetTrans = _target;
        lastReloadingVal = _reloading;

        HandlePull();

        float _curve = Mathf.Pow(Mathf.Cos(lastReloadingVal * Mathf.PI), cosPow)-1;
        transform.position = lastTargetTrans.position + pullPos + Vector3.up * displacementAmount * _curve;
        transform.rotation = lastTargetTrans.rotation * Quaternion.Euler(pullRot.x + rotationAmount * _curve, pullRot.y, pullRot.z);
    }

    void HandlePull()
    {
        pullAnimTime += Time.deltaTime;
        float s = Mathf.Clamp(pullAnimTime / pulloutTime, 0f, 1f);
        pullPos = Vector3.Lerp(pulloutPosOffset, Vector3.zero, easeOutCirc(s));
        pullRot = Vector3.Lerp(pulloutRotOffset, Vector3.zero, easeOutCirc(s));


        //if (pullAnimTime > pulloutTime) ReadyPull = true;
    }
    
    float easeOutCirc(float x) {
        //return Mathf.Sqrt(1 - Mathf.Pow(x - 1, 2));
        return x == 1 ? 1 : 1 - Mathf.Pow(2, -10 * x);
    }

    public void Pullout(float _pulloutTime)
    {
        pullAnimTime = 0f;
        pulloutTime = _pulloutTime;
        UpdateTransform(lastTargetTrans, lastReloadingVal);
    }
}
