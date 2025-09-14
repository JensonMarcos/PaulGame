using UnityEngine;

public class SpectatorMovement : MonoBehaviour
{
	public Transform bodyTransform;
    public float sensitivity = 1f;
    public Vector3 realRotation;


    void Start() {
		// Lock the mouse
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible   = false;

		//if(PlayerPrefs.HasKey("sensitivity")) sensitivity = PlayerPrefs.GetFloat("sensitivity");
	}

    void Update()
	{
		//check sense change
		// if(PlayerPrefs.HasKey("sensitivity") && PlayerPrefs.GetFloat("sensitivity") != sensitivity) sensitivity = PlayerPrefs.GetFloat("sensitivity");
		// if(PlayerManager.instance.paused) return;

		// Input
		float xMovement = Input.GetAxisRaw("Mouse X") * sensitivity;
		float yMovement = -Input.GetAxisRaw("Mouse Y") * sensitivity;

		// Calculate rotation from input
		realRotation = new Vector3(Mathf.Clamp(realRotation.x + yMovement, -90f, 90f), realRotation.y + xMovement, 0);

		bodyTransform.eulerAngles = Vector3.Scale(realRotation, new Vector3(0f, 1f, 0f));

        //Apply rotation to cam parent
		transform.eulerAngles = realRotation;
	}

}
