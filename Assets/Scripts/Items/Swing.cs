using System.Collections;
//using Unity.Netcode;
using UnityEngine;

public class Swing : MonoBehaviour, IItemAction
{
    [SerializeField] HandData RHand, LHand;

    Transform parent;
    PlayerAnimations anim;
    Transform cam;

    [SerializeField] float swingSpeed, retractSpeed;
    [SerializeField] float swingHoldTime;
    [SerializeField] float swingDistance = 0.55f;
    [SerializeField] Vector3 swingOffset;
    [SerializeField] float arcHeight = 0.15f;
    [SerializeField] Vector3 swingStartRot;
    [SerializeField] Vector3 swingEndRot;
    [SerializeField] float tiltAmount;

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


        StartCoroutine(SwingAnimation(RHand, -2f));
        //PunchServerRpc(handedness);
    }

    public void OnRightClick()
    {

    }

    IEnumerator SwingAnimation(HandData hand, float tiltMult)
    {
        Vector3 swingPos;
        var startSwing = Quaternion.Euler(swingStartRot);
        var endSwing = Quaternion.Euler(swingEndRot);

        var t = 0f;
        var x = 0f;
        while (x < 1)
        {
            yield return new WaitForEndOfFrame();
            x += swingSpeed * Time.deltaTime;
            //t = 1 - Mathf.Cos((x * Mathf.PI) / 2); //ease in lerping function
            t = 2.70158f * x * x * x - 1.70158f * x * x;

            swingPos = parent.InverseTransformPoint(cam.forward * swingDistance + cam.position) + swingOffset;

            // Arc up on the way out
            var arc = parent.InverseTransformDirection(cam.up) * Mathf.Sin(x * Mathf.PI) * arcHeight;
            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, swingPos, t) + arc;

            // Quick to start rot, then near end of swing to end rot
            if (x < 0.2f)
                hand.transform.localRotation = Quaternion.Lerp(hand.startRot, startSwing, x / 0.2f);
            else if (x < 0.7f)
                hand.transform.localRotation = startSwing;
            else
                hand.transform.localRotation = Quaternion.Lerp(startSwing, endSwing, (x - 0.7f) / 0.3f);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
        }


        yield return new WaitForSeconds(swingHoldTime);

        t = 1f;
        x = 1f;
        while (x > 0)
        {
            yield return new WaitForEndOfFrame();
            x -= retractSpeed * Time.deltaTime;
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2; //ease in lerping function

            swingPos = parent.InverseTransformPoint(cam.forward * swingDistance + cam.position) + swingOffset;

            // Arc down on the way back
            var arc = parent.InverseTransformDirection(cam.up) * -Mathf.Sin(x * Mathf.PI) * arcHeight;
            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, swingPos, t) + arc;
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, endSwing, x);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;

        anim.SetUpperBodyTilt(0f);

        
    }

}
