using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public float sensitivity = 1f;
    public Vector3 realRotation;
    [SerializeField] Camera cam;
    [SerializeField] float defaultFov, FovChangeSpeed;
    float targetFov, FovSensitivity;

    [Header("Camera Animations")]
    public Vector3 targetRot;
    [SerializeField] Vector3 offsetRot;
    [SerializeField] float deathCamRotSpeed = 12f;


    public void Initialize(Transform target, bool owner)
    {
        if(!owner) cam.gameObject.SetActive(false);

        transform.position = target.position;
        transform.rotation = target.rotation;
        realRotation = transform.eulerAngles;

        if(owner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void UpdateRotation(Vector2 inputs, ItemData _data)
    {
        FovSensitivity = cam.fieldOfView / defaultFov;

        float xMovement = inputs.x * sensitivity * FovSensitivity * 0.1f;
        float yMovement = -inputs.y * sensitivity * FovSensitivity * 0.1f;

        // Calculate rotation from input
        realRotation = new Vector3(Mathf.Clamp(realRotation.x + yMovement, -89.9f, 89.9f), realRotation.y + xMovement, 0);

        //cam offset
        targetRot = Vector3.Lerp(targetRot, Vector3.zero, _data.returnSpeed * Time.deltaTime);
        offsetRot = Vector3.Slerp(offsetRot, targetRot, _data.snap * Time.deltaTime);

        //Apply rotation to body
        Vector3 newRot = realRotation + offsetRot;
        newRot = new Vector3(Mathf.Clamp(newRot.x, -89.9f, 89.9f), newRot.y, newRot.z);
        transform.eulerAngles = newRot;
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }

    public void UpdateDeathCamRotation(Transform target)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target.forward), deathCamRotSpeed * Time.deltaTime);
    }

    public void AddRotation(float x, float y, float z, float mult)
    {
        targetRot += new Vector3(x, y, z) * mult;
    }

    public void UpdateCam(float _targetFov, float _weight)
    {
        targetFov = Mathf.Lerp(defaultFov, _targetFov, _weight);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, FovChangeSpeed * Time.deltaTime);
    }
}
