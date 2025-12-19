using Unity.Netcode;
using UnityEngine;

public class ThirdPersonAnimation : NetworkBehaviour
{
    Animator playerAnim;
    //MovementController movement;
    //Rigidbody rb;
    float legX, legY;
    public Transform scuffedFeet;

    void Start()
    {
        //movement = transform.root.GetComponent<MovementController>();
        playerAnim = GetComponent<Animator>();
        //rb = transform.root.GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        // localVelocity = localVelocity.normalized;

        // float mag = rb.linearVelocity.magnitude / movement.sprintSpeed;

        // legX = localVelocity.x * mag;
        // legY = localVelocity.z * mag;
        legX = scuffedFeet.localPosition.x;
        legY = scuffedFeet.localPosition.z;

        playerAnim.SetFloat("X", legX);
        playerAnim.SetFloat("Y", legY);
    }


}
