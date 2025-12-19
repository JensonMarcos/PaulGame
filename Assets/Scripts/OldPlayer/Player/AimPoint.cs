using UnityEngine;

public class AimPoint : MonoBehaviour
{
    public Transform FPAimPoint;
    public Transform Body;

    void FixedUpdate()
    {
        transform.position = FPAimPoint.position + Body.forward;
    }
}
