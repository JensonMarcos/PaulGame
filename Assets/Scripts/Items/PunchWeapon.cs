using System.Collections;
using UnityEngine;

public class PunchWeapon : MeleeWeapon
{
    [Header("Punch")]
    [SerializeField] HandData rightPunch;
    [SerializeField] HandData leftPunch;
    [SerializeField] float punchSpeed, retractSpeed;
    [SerializeField] float punchHoldTime;
    [SerializeField] float punchDistance = 0.55f;
    [SerializeField] Vector3 punchEndRot;
    [SerializeField] float tiltAmount;
    [SerializeField] float rotWeight;
    [SerializeField] float uppercutArc = 0.2f;
    [SerializeField] Vector3 uppercutEndRot;

    [Space]
    [SerializeField] Vector3 ChargeOffset;
    [SerializeField] Vector3 ChargeRot;
    [SerializeField] float ChargeTilt = 10f;

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

    public override void PlayCharge()
    {
        Cache();
        //StopAttackRoutine();
        StopCharge();

        chargeRoutine = StartCoroutine(ChargeAnimation(
            handedness ? rightPunch : leftPunch,
            handedness ? 1f : -1.5f,
            handedness ? 1f : -1f));
    }

    public override void PlayAttack(bool uppercut = false)
    {
        Cache();
        //StopAttackRoutine();
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        bool right = handedness;
        HandData hand = right ? rightPunch : leftPunch;
        float tilt = right ? 2.5f : -1f;
        float side = right ? 1f : -1f;
        attackRoutine = StartCoroutine(uppercut
            ? UppercutAnimation(hand, tilt, side, right)
            : PunchAnimation(hand, tilt, side, right));
        handedness = !handedness;
    }

    public override void StopCharge()
    {
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }
        if (!cached) return;

        rightPunch.transform.localPosition = rightPunch.startPos;
        rightPunch.transform.localRotation = rightPunch.startRot;
        leftPunch.transform.localPosition = leftPunch.startPos;
        leftPunch.transform.localRotation = leftPunch.startRot;
        anim.SetUpperBodyTilt(0f);
        anim.ResetFirstBoneWeights();
    }

    IEnumerator ChargeAnimation(HandData hand, float tiltMult, float side)
    {
        var t = 0f;
        var x = 0f;
        while (x < 1f)
        {
            yield return new WaitForEndOfFrame();
            x += Time.deltaTime / ChargeTime;
            t = -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(x)) - 1f) / 2f;

            Vector3 pos = hand.startPos + parent.InverseTransformDirection(cam.forward * ChargeOffset.x + cam.up * ChargeOffset.y + cam.right * ChargeOffset.z * side);
            Quaternion mirroredRot = Quaternion.Euler(ChargeRot.x, ChargeRot.y * side, ChargeRot.z * side);
            Quaternion rot = Quaternion.Inverse(parent.rotation) * cam.rotation * mirroredRot;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, pos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, rot, t * rotWeight);
            anim.SetUpperBodyTilt(Mathf.Lerp(0f, ChargeTilt * tiltMult, t));
        }
    }


    IEnumerator PunchAnimation(HandData hand, float tiltMult, float side, bool right)
    {
        Vector3 fromPos = hand.transform.localPosition;
        Quaternion fromRot = hand.transform.localRotation;
        Quaternion mirroredRot = Quaternion.Euler(punchEndRot.x, punchEndRot.y * side, punchEndRot.z * side);
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
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * mirroredRot;

            hand.transform.localPosition = Vector3.LerpUnclamped(fromPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(fromRot, punchRot, x * rotWeight);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
            anim.SetFirstBoneWeight(right, t);
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
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * mirroredRot;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, punchPos, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x * rotWeight);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
            anim.SetFirstBoneWeight(right, t);
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;
        anim.SetUpperBodyTilt(0f);
        anim.SetFirstBoneWeight(right, 0f);
        attackRoutine = null;
    }

    IEnumerator UppercutAnimation(HandData hand, float tiltMult, float side, bool right)
    {
        Vector3 fromPos = hand.transform.localPosition;
        Quaternion fromRot = hand.transform.localRotation;
        Quaternion mirroredRot = Quaternion.Euler(uppercutEndRot.x, uppercutEndRot.y * side, uppercutEndRot.z * side);
        Vector3 punchPos;
        Quaternion punchRot;

        var t = 0f;
        var x = 0f;
        while (x < 1)
        {
            yield return new WaitForEndOfFrame();
            x += punchSpeed * Time.deltaTime * 1.1f;
            t = 2.70158f * x * x * x - 1.70158f * x * x;

            punchPos = parent.InverseTransformPoint(cam.forward * punchDistance + cam.position);
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * mirroredRot;
            var arc = parent.InverseTransformDirection(cam.up) * -Mathf.Sin(Mathf.Clamp01(x) * Mathf.PI) * uppercutArc;

            hand.transform.localPosition = Vector3.LerpUnclamped(fromPos, punchPos, t) + arc;
            hand.transform.localRotation = Quaternion.Lerp(fromRot, punchRot, x * rotWeight);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
            anim.SetFirstBoneWeight(right, t);
        }

        yield return new WaitForSeconds(punchHoldTime);

        float upTime = 0.5f / Mathf.Max(retractSpeed, 0.01f);
        float u = 0f;
        while (u < 1f)
        {
            yield return new WaitForEndOfFrame();
            u += Time.deltaTime / upTime;
            float e = -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(u)) - 1f) / 2f;

            punchPos = parent.InverseTransformPoint(cam.forward * punchDistance + cam.position);
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * mirroredRot;
            Vector3 up = parent.InverseTransformDirection(cam.up) * uppercutArc;

            hand.transform.localPosition = punchPos + up * e;
            hand.transform.localRotation = Quaternion.Lerp(fromRot, punchRot, rotWeight);
            anim.SetUpperBodyTilt(tiltAmount * tiltMult);
            anim.SetFirstBoneWeight(right, 1f);
        }

        t = 1f;
        x = 1f;
        while (x > 0)
        {
            yield return new WaitForEndOfFrame();
            x -= retractSpeed * Time.deltaTime;
            t = -(Mathf.Cos(Mathf.PI * x) - 1) / 2;

            punchPos = parent.InverseTransformPoint(cam.forward * punchDistance + cam.position);
            punchRot = Quaternion.Inverse(parent.rotation) * cam.rotation * mirroredRot;
            Vector3 raised = punchPos + parent.InverseTransformDirection(cam.up) * uppercutArc;

            hand.transform.localPosition = Vector3.LerpUnclamped(hand.startPos, raised, t);
            hand.transform.localRotation = Quaternion.Lerp(hand.startRot, punchRot, x * rotWeight);

            anim.SetUpperBodyTilt(Mathf.Lerp(0, tiltAmount * tiltMult, t));
            anim.SetFirstBoneWeight(right, t);
        }

        hand.transform.localPosition = hand.startPos;
        hand.transform.localRotation = hand.startRot;
        anim.SetUpperBodyTilt(0f);
        anim.SetFirstBoneWeight(right, 0f);
        attackRoutine = null;
    }
}
