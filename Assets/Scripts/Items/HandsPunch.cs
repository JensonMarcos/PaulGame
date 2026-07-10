using System.Collections;
//using Unity.Netcode;
using UnityEngine;

[System.Serializable]
public struct HandData
{
    public Transform transform;
    public Vector3 startPos;
    public Quaternion startRot;
}

public class HandsPunch : MonoBehaviour, IItemAction
{
    [SerializeField] HandData RHand, LHand;

    Transform parent;
    PlayerAnimations anim;
    Transform cam;

    [SerializeField] float punchSpeed, retractSpeed;
    [SerializeField] float punchHoldTime;
    [SerializeField] float punchDistance = 0.55f;
    [SerializeField] Vector3 punchEndRot = new Vector3(90, 0, 0);
    [SerializeField] float tiltAmount;
    [SerializeField] float rotWeight;

    bool handedness;

    void Start()
    {
        parent = transform.parent;
        anim = transform.root.GetComponent<PlayerAnimations>();
        cam = anim.cam.transform;

        RHand.startPos = RHand.transform.localPosition;
        RHand.startRot = RHand.transform.localRotation;
        LHand.startPos = LHand.transform.localPosition;
        LHand.startRot = LHand.transform.localRotation;
    }

    public void OnLeftClick()
    {
        //StopCoroutine("PunchAnimation");

        //ResetHands();

        anim.SetUpperBodyTilt(0f);

        handedness = !handedness;

        StartCoroutine(PunchAnimation(handedness ? RHand : LHand, handedness ? -2f : 1f));
        //PunchServerRpc(handedness);
    }

    public void OnRightClick()
    {
        
    }

    // [Rpc(SendTo.Server)]
    // public void PunchServerRpc(bool _handedness)
    // {
    //     PunchClientRpc(_handedness);
    // }

    // [Rpc(SendTo.ClientsAndHost)]
    // public void PunchClientRpc(bool _handedness)
    // {
    //     if(IsOwner) return;
    //     StartCoroutine(PunchAnimation(_handedness ? RHand : LHand, _handedness ? -2f : 1f));
    // }

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

            punchPos = parent.InverseTransformPoint(cam.forward * punchDistance + cam.position);
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * Quaternion.Euler(punchEndRot);

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x * rotWeight);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
        }


        yield return new WaitForSeconds(punchHoldTime);

        t = 1f;
        x = 1f;
        while (x > 0)
        {
            yield return new WaitForEndOfFrame();
            x -= retractSpeed * Time.deltaTime;
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2; //ease in lerping function

            punchPos = parent.InverseTransformPoint(cam.forward * punchDistance + cam.position);
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * Quaternion.Euler(punchEndRot);

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x * rotWeight);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;

        anim.SetUpperBodyTilt(0f);

        
    }

}
