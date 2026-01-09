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
    [SerializeField] Transform cam;
    [SerializeField] LayerMask groundMask;
    [SerializeField] float rotateSpeed;


    public void UpdatePosition(Vector3 target)
    {
        targetOffset = target;

        if (Physics.SphereCast(transform.position, checkRadius, transform.forward, out RaycastHit hit, moveDistance, groundMask.value))
        {
            Debug.DrawRay(transform.position, hit.point - transform.position, Color.red);

            float proximity = Vector3.Distance(hit.point, transform.position);

            targetOffset += new Vector3(0, 0, Mathf.Max(proximity - moveDistance, -maxMoveDistance));
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetOffset, moveSpeed * Time.deltaTime);
    }

    public void UpdateRotation(bool aim, float weight)
    {
        Quaternion targetRot;
        if (aim)
        {
            Vector3 aimTarget = cam.position + cam.forward * 100f;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 100f, groundMask.value))
            {
                aimTarget = hit.point;
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

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime * weight);
        transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.localEulerAngles.y, 0f);
    }

    public void AddTransform(Vector3 pos)
    {
        transform.localPosition += pos;
    }
}
