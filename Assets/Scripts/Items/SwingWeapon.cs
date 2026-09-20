using System.Collections;
using UnityEngine;

public class SwingWeapon : MeleeWeapon
{
    [SerializeField] HandData rightSwing, leftSwing;
    [SerializeField] float swingSpeed, retractSpeed;
    [SerializeField] float swingHoldTime;
    [SerializeField] float swingDistance = 0.55f;
    [SerializeField] Vector3 swingOffset;
    [SerializeField] float arcHeight = 0.15f;
    [SerializeField] Vector3 swingStartRot;
    [SerializeField] Vector3 swingEndRot;
    [SerializeField] float tiltAmount = 15f;

    [SerializeField] Vector3 ChargeOffset;
    [SerializeField] Vector3 ChargeRot;
    [SerializeField] float ChargeTilt = 10f;

    Transform parent;
    protected PlayerAnimations anim;
    Transform cam;
    bool cached;

    protected void Cache()
    {
        if (cached) return;
        parent = transform.parent;
        anim = transform.root.GetComponent<PlayerAnimations>();
        cam = anim.cam.transform;
        rightSwing.startPos = rightSwing.transform.localPosition;
        rightSwing.startRot = rightSwing.transform.localRotation;
        if (leftSwing.transform != null)
        {
            leftSwing.startPos = leftSwing.transform.localPosition;
            leftSwing.startRot = leftSwing.transform.localRotation;
        }
        cached = true;
    }

    public override void PlayCharge()
    {
        Cache();
        if (chargeRoutine != null) StopCoroutine(chargeRoutine);
        chargeRoutine = StartCoroutine(ChargeAnimation(rightSwing, 1f));
    }

    public override void PlayAttack()
    {
        Cache();
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }
        attackRoutine = StartCoroutine(SwingAnimation(rightSwing, 1f));
    }

    public override void StopCharge()
    {
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }
        if (!cached) return;

        rightSwing.transform.localPosition = rightSwing.startPos;
        rightSwing.transform.localRotation = rightSwing.startRot;
        if (leftSwing.transform != null)
        {
            leftSwing.transform.localPosition = leftSwing.startPos;
            leftSwing.transform.localRotation = leftSwing.startRot;
        }
        anim.SetUpperBodyTilt(0f);
    }

    IEnumerator ChargeAnimation(HandData hand, float tilt)
    {
        var t = 0f;
        var x = 0f;
        while (x < 1f)
        {
            yield return new WaitForEndOfFrame();
            x += Time.deltaTime / ChargeTime;
            t = -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(x)) - 1f) / 2f;

            Vector3 pos = parent.InverseTransformPoint(cam.position + cam.forward * ChargeOffset.x + cam.up * ChargeOffset.y + cam.right * ChargeOffset.z);
            Quaternion rot = Quaternion.Inverse(parent.rotation) * cam.rotation * Quaternion.Euler(ChargeRot);

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, pos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, rot, t);
            anim.SetUpperBodyTilt(Mathf.Lerp(0f, ChargeTilt * tilt, t));
        }

    }

    IEnumerator SwingAnimation(HandData hand, float tiltMult)
    {
        Vector3 fromPos = hand.transform.localPosition;
        Quaternion fromRot = hand.transform.localRotation;
        Vector3 swingPos;
        var startSwing = Quaternion.Euler(swingStartRot);
        var endSwing = Quaternion.Euler(swingEndRot);

        var t = 0f;
        var x = 0f;
        while (x < 1)
        {
            yield return new WaitForEndOfFrame();
            x += swingSpeed * Time.deltaTime;
            t = 2.70158f * x * x * x - 1.70158f * x * x;

            swingPos = parent.InverseTransformPoint(cam.forward * swingDistance + cam.position) + swingOffset;
            var arc = parent.InverseTransformDirection(cam.up) * Mathf.Sin(x * Mathf.PI) * arcHeight;
            hand.transform.localPosition = Vector3.LerpUnclamped(fromPos, swingPos, t) + arc;

            if (x < 0.2f)
                hand.transform.localRotation = Quaternion.Lerp(fromRot, startSwing, x / 0.2f);
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
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2;

            swingPos = parent.InverseTransformPoint(cam.forward * swingDistance + cam.position) + swingOffset;
            var arc = parent.InverseTransformDirection(cam.up) * -Mathf.Sin(x * Mathf.PI) * arcHeight;
            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, swingPos, t) + arc;
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, endSwing, x);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;
        anim.SetUpperBodyTilt(0f);
        attackRoutine = null;
    }
}
