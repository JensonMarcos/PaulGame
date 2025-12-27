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

    public void UpdateTransform(Transform target)//, float noZRot)
    {
        HandlePull();

        transform.position = target.position + pullPos;
        transform.rotation = target.rotation * Quaternion.Euler(pullRot);
    }

    // public void ResetHands()
    // {
    //     //StopCoroutine("PunchAnimation");
    //     // LHand.hand.localPosition = LHand.startPos;
    //     // LHand.hand.localRotation = LHand.startRot;
    //     // RHand.hand.localPosition = RHand.startPos;
    //     // RHand.hand.localRotation = RHand.startRot;
    //     // readyPunch = true;
    // }

    // public void Punch()
    // {
    //     if(!readyPunch) return;

    //     StopCoroutine("PunchAnimation");

    //     ResetHands();

    //     body.UpperBodyTilt = 0f;

    //     whichhand = !whichhand;

    //     StartCoroutine(PunchAnimation(whichhand ? RHand : LHand, whichhand ? -2f : 1f));
    // }

    // IEnumerator PunchAnimation(HandRig rig, float tiltMult)
    // {
    //     readyPunch = false;

    //     Vector3 punchPos;
    //     Quaternion punchRot;

    //     var t = 0f;
    //     var x = 0f;
    //     while (x < 1)
    //     {
    //         x += punchSpeed * Time.deltaTime;
    //         //t = 1 - Mathf.Cos((x * Mathf.PI) / 2); //ease in lerping function
    //         t = 2.70158f * x * x * x - 1.70158f * x * x;

    //         punchPos = HandParent.InverseTransformPoint(punchTarget.position);
    //         punchRot = Quaternion.Inverse(HandParent.rotation) * punchTarget.rotation;

    //         rig.hand.localPosition = Vector3.LerpUnclamped(rig.startPos, punchPos, t);
    //         rig.hand.localRotation = Quaternion.Lerp(rig.startRot, punchRot, x);

    //         body.UpperBodyTilt = Mathf.Lerp(0, tiltAmount * tiltMult, t);
    //         yield return null;
    //     }

    //     yield return new WaitForSeconds(punchHoldTime);

    //     // punchPos = HandParent.InverseTransformPoint(rig.IK.transform.position);
    //     // punchRot = Quaternion.Inverse(HandParent.rotation) * rig.IK.transform.rotation;

    //     readyPunch = true;

    //     t = 1f;
    //     x = 1f;
    //     while (x > 0)
    //     {
    //         x -= punchSpeed * Time.deltaTime;
    //         t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2; //ease in lerping function

    //         punchPos = HandParent.InverseTransformPoint(punchTarget.position);
    //         punchRot = Quaternion.Inverse(HandParent.rotation) * punchTarget.rotation;

    //         rig.hand.localPosition = Vector3.LerpUnclamped(rig.startPos, punchPos, t);
    //         rig.hand.localRotation = Quaternion.Lerp(rig.startRot, punchRot, x);

    //         body.UpperBodyTilt = Mathf.Lerp(0, tiltAmount * tiltMult, t);
    //         yield return null;
    //     }

    //     rig.hand.localPosition = rig.startPos;
    //     rig.hand.localRotation = rig.startRot;

    //     body.UpperBodyTilt = 0f;

        
    // }

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
    }
}
