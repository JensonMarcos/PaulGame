using System.Collections;
using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public struct HandData
{
    public Transform transform;
    public Vector3 startPos;
    public Quaternion startRot;
}

public class HandsPunch : NetworkBehaviour, IItemAction
{
    [SerializeField] HandData RHand, LHand;

    [SerializeField] Transform handParent;
    [SerializeField] Transform punchTarget;

    [SerializeField] BodyAnimation body;

    [SerializeField] float punchSpeed;
    [SerializeField] float punchHoldTime;
    [SerializeField] float tiltAmount;

    [SerializeField] bool readyPunch;
    [SerializeField] bool handedness;

    void Start()
    {
        RHand.startPos = RHand.transform.localPosition;
        RHand.startRot = RHand.transform.localRotation;
        LHand.startPos = LHand.transform.localPosition;
        LHand.startRot = LHand.transform.localRotation;

        readyPunch = true;
    }

    public void OnLeftClick()
    {
        if(!readyPunch) return;

        StopCoroutine("PunchAnimation");

        //ResetHands();

        body.UpperBodyTilt = 0f;

        handedness = !handedness;

        StartCoroutine(PunchAnimation(handedness ? RHand : LHand, handedness ? -2f : 1f));
        PunchServerRpc(handedness);
    }

    public void OnRightClick()
    {
        
    }

    [ServerRpc(RequireOwnership = true)]
    public void PunchServerRpc(bool _handedness)
    {
        PunchClientRpc(_handedness);
    }

    [ClientRpc]
    public void PunchClientRpc(bool _handedness)
    {
        if(IsOwner) return;
        StartCoroutine(PunchAnimation(_handedness ? RHand : LHand, _handedness ? -2f : 1f));
    }

    IEnumerator PunchAnimation(HandData hand, float tiltMult)
    {
        readyPunch = false;

        Vector3 punchPos;
        Quaternion punchRot;

        var t = 0f;
        var x = 0f;
        while (x < 1)
        {
            x += punchSpeed * Time.deltaTime;
            //t = 1 - Mathf.Cos((x * Mathf.PI) / 2); //ease in lerping function
            t = 2.70158f * x * x * x - 1.70158f * x * x;

            punchPos = handParent.InverseTransformPoint(punchTarget.position);
            punchRot = Quaternion.Inverse(handParent.rotation) * punchTarget.rotation;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x);

            body.UpperBodyTilt = Mathf.Lerp(0, tiltAmount * tiltMult, t);
            yield return null;
        }

        yield return new WaitForSeconds(punchHoldTime);

        // punchPos = HandParent.InverseTransformPoint(rig.IK.transform.position);
        // punchRot = Quaternion.Inverse(HandParent.rotation) * rig.IK.transform.rotation;

        readyPunch = true;

        t = 1f;
        x = 1f;
        while (x > 0)
        {
            x -= punchSpeed * Time.deltaTime;
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2; //ease in lerping function

            punchPos = handParent.InverseTransformPoint(punchTarget.position);
            punchRot = Quaternion.Inverse(handParent.rotation) * punchTarget.rotation;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x);

            body.UpperBodyTilt = Mathf.Lerp(0, tiltAmount * tiltMult, t);
            yield return null;
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;

        body.UpperBodyTilt = 0f;

        
    }

}
