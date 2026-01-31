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

    [SerializeField] float punchSpeed, retractSpeed;
    [SerializeField] float punchHoldTime;
    [SerializeField] float tiltAmount;
    [SerializeField] float rotWeight;

    [SerializeField] bool handedness;

    void Start()
    {
        RHand.startPos = RHand.transform.localPosition;
        RHand.startRot = RHand.transform.localRotation;
        LHand.startPos = LHand.transform.localPosition;
        LHand.startRot = LHand.transform.localRotation;
    }

    public void OnLeftClick()
    {
        //StopCoroutine("PunchAnimation");

        //ResetHands();

        body.UpperBodyTilt = 0f;

        handedness = !handedness;

        StartCoroutine(PunchAnimation(handedness ? RHand : LHand, handedness ? -2f : 1f));
        PunchServerRpc(handedness);
    }

    public void OnRightClick()
    {
        
    }

    [Rpc(SendTo.Server)]
    public void PunchServerRpc(bool _handedness)
    {
        PunchClientRpc(_handedness);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PunchClientRpc(bool _handedness)
    {
        if(IsOwner) return;
        StartCoroutine(PunchAnimation(_handedness ? RHand : LHand, _handedness ? -2f : 1f));
    }

    IEnumerator PunchAnimation(HandData hand, float tiltMult)
    {
        Vector3 punchPos;
        Quaternion punchRot;

        var t = 0f;
        var x = 0f;
        while (x < 1)
        {
            yield return new WaitForEndOfFrame();
            x += punchSpeed * Time.deltaTime;
            //t = 1 - Mathf.Cos((x * Mathf.PI) / 2); //ease in lerping function
            t = 2.70158f * x * x * x - 1.70158f * x * x;

            punchPos = handParent.InverseTransformPoint(punchTarget.position);
            punchRot = Quaternion.Inverse(handParent.rotation) * punchTarget.rotation;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x * rotWeight);

            body.UpperBodyTilt = Mathf.Lerp(0, tiltAmount * tiltMult, t);
        }


        yield return new WaitForSeconds(punchHoldTime);

        t = 1f;
        x = 1f;
        while (x > 0)
        {
            yield return new WaitForEndOfFrame();
            x -= retractSpeed * Time.deltaTime;
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2; //ease in lerping function

            punchPos = handParent.InverseTransformPoint(punchTarget.position);
            punchRot = Quaternion.Inverse(handParent.rotation) * punchTarget.rotation;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x * rotWeight);

            body.UpperBodyTilt = Mathf.Lerp(0, tiltAmount * tiltMult, t);
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;

        body.UpperBodyTilt = 0f;

        
    }

}
