using UnityEngine;

public class ItemAnimation : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] Vector3 targetOffset;
    //[SerializeField] Transform aimTarget;
    [SerializeField] float moveDistance;
    [SerializeField] float maxMoveDistance;
    [SerializeField] float checkRadius;
    [SerializeField] float moveSpeed;

    [Space]
    [Header("Rotation")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] float rotateSpeed, aimSpeedMult;

    [Space]
    [Header("Reload")]
    [SerializeField] float reloadDisplacement = 0.2f;
    [SerializeField] float reloadRotation = -30f;
    [SerializeField] float reloadCosPow = 10f;

    Transform cam;
    Vector3 reloadPos;
    Quaternion reloadRot = Quaternion.identity;

    public void Initialize(Transform _cam)
    {
        cam = _cam;
    }

    public void UpdatePosition(Vector3 target, bool isOwner)
    {
        RemoveReload();

        targetOffset = target;
        float scale = transform.lossyScale.z;

        if (isOwner && Physics.SphereCast(transform.position, checkRadius * scale, transform.forward, out RaycastHit hit, moveDistance * scale, groundMask.value))
        {
            Debug.DrawRay(transform.position, hit.point - transform.position, Color.red);

            float proximity = Vector3.Distance(hit.point, transform.position) / scale;

            targetOffset += new Vector3(0, 0, Mathf.Max(proximity - moveDistance, -maxMoveDistance));
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetOffset, moveSpeed * Time.deltaTime);
    }

    public void UpdateRotation(bool aim, float aimingValue, float weight, bool isOwner)
    {
        Quaternion targetRot;
        if (aim)
        {
            Vector3 aimTarget = cam.position + cam.forward * 100f;
            if (isOwner && Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 100f, groundMask.value))
            {
                aimTarget = Vector3.Lerp(hit.point, aimTarget, Mathf.Pow(aimingValue, 2));
            }

            targetRot = Quaternion.LookRotation(aimTarget - transform.position);
            //targetRot.eulerAngles = new Vector3(targetRot.eulerAngles.x, targetRot.eulerAngles.y, transform.eulerAngles.z);
        }
        else targetRot = transform.parent.rotation * Quaternion.Euler(Vector3.zero);

        // if (aimLock)
        // {
        //     transform.rotation = targetRot;
        //     return;
        // }
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * weight * (aimingValue * aimSpeedMult + 1) * Time.deltaTime);
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, 0f);
    }

    //must run after UpdatePosition/UpdateRotation, which strip last frame's reload offset before easing
    public void UpdateReload(float reloading)
    {
        float curve = Mathf.Pow(Mathf.Cos(reloading * Mathf.PI), reloadCosPow) - 1f;
        reloadRot = Quaternion.Euler(reloadRotation * curve, 0f, 0f);
        reloadPos = transform.parent.InverseTransformDirection(Vector3.up) * reloadDisplacement * curve;

        //pivots around the parent origin so the dip arcs like it did on the hands rig
        transform.localPosition = reloadRot * transform.localPosition + reloadPos;
        transform.localRotation = reloadRot * transform.localRotation;
    }

    void RemoveReload()
    {
        Quaternion inverse = Quaternion.Inverse(reloadRot);
        transform.localPosition = inverse * (transform.localPosition - reloadPos);
        transform.localRotation = inverse * transform.localRotation;
        reloadPos = Vector3.zero;
        reloadRot = Quaternion.identity;
    }

    public void AddTransform(Vector3 pos, Quaternion rot)
    {
        transform.localPosition += pos;
        transform.localRotation *= rot;
    }
}
