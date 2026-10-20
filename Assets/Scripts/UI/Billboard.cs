using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] bool doYRotation = true;
    
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 dir = cam.transform.position - transform.position;
        if (!doYRotation) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir);
    }
}
