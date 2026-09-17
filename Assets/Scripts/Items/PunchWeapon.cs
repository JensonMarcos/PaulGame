using System.Collections;
using UnityEngine;

public class PunchWeapon : MeleeWeapon
{
    [SerializeField] HandData rightPunch, leftPunch;
    [SerializeField] float punchSpeed, retractSpeed;
    [SerializeField] float punchHoldTime;
    [SerializeField] float punchDistance = 0.55f;
    [SerializeField] Vector3 punchEndRot = new Vector3(90, 0, 0);
    [SerializeField] float tiltAmount;
    [SerializeField] float rotWeight;

    Transform parent;
    PlayerAnimations anim;
    Transform cam;
    bool handedness;
    bool cached;

    void Cache()
    {
        if (cached) return;
        parent = transform.parent;
        anim = transform.root.GetComponent<PlayerAnimations>();
        cam = anim.cam.transform;
        rightPunch.startPos = rightPunch.transform.localPosition;
        rightPunch.startRot = rightPunch.transform.localRotation;
        leftPunch.startPos = leftPunch.transform.localPosition;
        leftPunch.startRot = leftPunch.transform.localRotation;
        cached = true;
    }

    public override void PlayAttack()
    {
        Cache();
        anim.SetUpperBodyTilt(0f);
        handedness = !handedness;
        StartCoroutine(PunchAnimation(handedness ? rightPunch : leftPunch, handedness ? -1.5f : 1f));
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
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2;

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
