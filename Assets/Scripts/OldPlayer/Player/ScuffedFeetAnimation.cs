using UnityEngine;

public class ScuffedFeetAnimation : MonoBehaviour
{

    MovementController movement;
    Rigidbody rb;
    float legX, legY;
    [SerializeField] float lerpSpeed;

    void Start()
    {
        movement = transform.root.GetComponent<MovementController>();
        rb = transform.root.GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        localVelocity = localVelocity.normalized;

        float mag = rb.linearVelocity.magnitude / movement.walkSpeed;
        mag = Mathf.Clamp(mag, 0f, 1f);

        legX = localVelocity.x * mag;
        legY = localVelocity.z * mag;

        //transform.localPosition = Vector3.Lerp(transform.localPosition, new Vector3(legX, 0, legY), lerpSpeed * Time.fixedDeltaTime);
        transform.localPosition = new Vector3(legX, 0, legY);

    }


}
