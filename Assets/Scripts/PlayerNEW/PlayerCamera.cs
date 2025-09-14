using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public float sensitivity = 1f;
    public Vector3 realRotation;

    public void Initialize(Transform target)
    {
        transform.position = target.position;
        transform.rotation = target.rotation;
        realRotation = transform.eulerAngles;
        
        Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible   = false;
    }

    public void UpdateRotation()
    {
        float xMovement = Input.GetAxisRaw("Mouse X") * sensitivity;// * ADSsensitivity;
        float yMovement = -Input.GetAxisRaw("Mouse Y") * sensitivity;// * ADSsensitivity;

		// Calculate rotation from input
		realRotation = new Vector3(Mathf.Clamp(realRotation.x + yMovement, -90f, 90f), realRotation.y + xMovement, 0);

		//Apply rotation to body
		transform.eulerAngles = realRotation;
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }
}
