using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MovementController : MonoBehaviour
{
    [SerializeField] float currentSpeed;

    public Transform bodyTransform;
    public Transform camTransform;
    public Rigidbody rb;
    public BoxCollider playerCollider;
    public float colliderSize = 0.4f;
    
    public Vector3 velocity, targetPos;
    [SerializeField] bool extrapolate;
    Vector3 prevPos;
    float forwardMove;
    float sideMove;

    public bool grounded = false;
    public Vector3 groundNormal;

    public bool jumping;
    public bool crouching;
    public bool sprinting;
    public bool sliding;
    
    public float legDistance = 0.5f;
    public float legSpeed = 30f;
    public float legSize = 0.39f, legHeight = 0.1f;
    Vector3 legHalfExtent;
    public LayerMask groundLayer, normalLayer, spectatorLayer;
    public float maxSlopeAngle = 46f;
    RaycastHit hit;
    
    //for collisions
    Collider[] _colliders = new Collider [32];

    [Header("Inputs")]
    public float verticalAxis = 0f;
    public float horizontalAxis = 0f;
    public bool inputJumping = false;
    public bool inputCrouching = false;
    public bool inputSprinting = false;
    
    [Header ("Crouching Things")]
    public float defaultHeight = 2f;
    public float crouchingHeight = 1.5f;
    public float crouchingSpeed = 15f;
    float crouchingLerp, currentHeight;
    Vector3 crouchOffset, prevCrouchOffset;

    #region MovementConfig

    [Header ("Jumping and gravity")]
    public bool autoBhop = true;
    public float gravity = 20f;
    public float jumpForce = 7.07f;
    public float jumpCoolDown = 0.2f;
    public bool applyFrictionOnJump = true;
    float jumpTime;
    
    [Header ("General physics")]
    //public float maxSpeed = 6f;
    public float maxVelocity = 35f;
    public float coyoteTime = 0.12f;
    float cTime;

    
    [Header ("Ground movement")]
    public float walkSpeed = 7f;
    public float sprintSpeed = 11f;
    public float acceleration = 14f;
    public float deceleration = 10f;
    public float friction = 6f;
    float sprintAirDelay;

    [Header ("Air movement")]
    //public bool clampAirSpeed;
    public bool doAirFriction = true;
    public float airAcceleration = 6f;
    public float airFriction = 0.2f;
    public float airCap = 0.6f;

    [Header ("Crouch movement")]
    public float crouchSpeed = 4f;
    public float crouchAcceleration = 8f;
    public float crouchDeceleration = 4f;
    public float crouchFriction = 3f;

    [Header ("Sliding")]
    public float minimumSlideSpeed = 9f;
    public float maximumSlideSpeed = 20f;
    public float slideSpeedMultiplier = 1.05f;
    public float addedSlideSpeed = 3.5f;
    public float slideFriction = 1f;
    //public float downhillSlideSpeedMultiplier = 2.5f;
    public float slideDelay = 2f;
    float slideTime;

    #endregion

    private void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(targetPos + new Vector3(0, 1 - currentHeight + legHeight, 0), legHalfExtent * 2);
        Gizmos.DrawRay(targetPos + (1 - colliderSize) * Vector3.up, (currentHeight - legSize - colliderSize) * Vector3.down);

        Gizmos.color = Color.cyan;
        if(hit.collider != null) {
            Gizmos.DrawWireSphere(hit.point, 0.1f);
            Gizmos.DrawWireCube(targetPos + (1 - colliderSize) * Vector3.up + hit.distance * Vector3.down, legHalfExtent * 2);
        } 

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetPos + (1 - legHeight - 0.01f) * Vector3.up + (defaultHeight - currentHeight) * Vector3.up, legHalfExtent * 2);
    }

    void Start() {
        transform.position += Vector3.up;
        groundLayer = normalLayer;
    }

    // void Update() {

    //     if(Input.GetKeyDown(KeyCode.I)) extrapolate = (extrapolate ? false : true);
    //     polateText.text = (extrapolate ? "extrapolate" : "interpolate");

    //     float lerpFactor = ((Time.time-Time.fixedTime)/Time.fixedDeltaTime);
    //     if(extrapolate) {
    //         transform.position = targetPos + (velocity*lerpFactor*Time.fixedDeltaTime);
    //     } else {
    //         transform.position = Vector3.Lerp(prevPos, targetPos, lerpFactor);
    //     }

    // }

    void FixedUpdate() {
        legHalfExtent = new Vector3(legSize, legHeight, legSize);

        HandleInputs();

        //prevPos = targetPos;
        //velocity = rb.linearVelocity;
        targetPos = transform.position;

        CheckGrounded();
        HangleGrounding();
        HangleCrouch();
        ProcessMovement();
        HangleGrounding();
        

        currentSpeed = new Vector3 (velocity.x, 0f, velocity.z).magnitude;

        //if (velocity.sqrMagnitude != 0f) targetPos += velocity * Time.fixedDeltaTime;
        //DoCollisions(1f);
        
        rb.linearVelocity = velocity + (targetPos - transform.position)/Time.fixedDeltaTime;
        //rb.MovePosition(targetPos);
    }
    
    void HandleInputs() {
        verticalAxis = Input.GetAxisRaw("Vertical");
        horizontalAxis = Input.GetAxisRaw("Horizontal");
        inputJumping = Input.GetButton("Jump");
        inputCrouching = Input.GetButton("Crouch");
        inputSprinting = Input.GetButton("Sprint");

        sprinting = inputSprinting && sprintAirDelay < 0.15f && !crouching && verticalAxis > 0 && new Vector3(velocity.x, 0f, velocity.z).magnitude >= sprintSpeed - 0.25f;
    }

    void ProcessMovement() {
        //jump cooldown
        if(jumpTime < jumpCoolDown) jumpTime += Time.deltaTime;
        if(slideTime < slideDelay) slideTime += Time.deltaTime;
        if(cTime < coyoteTime) cTime += Time.deltaTime;
        if(sprintAirDelay < 0.15f) sprintAirDelay += Time.deltaTime;

        //if in air
        if(!grounded) { //IGNORE ALL THE GROUND CHECKS
            //gravity
            velocity.y -= gravity * Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y, -50f);
            
            sliding = false;

            if(cTime < coyoteTime && inputJumping && jumpTime >= jumpCoolDown && velocity.y <= 0) {
                Jump();
                sprintAirDelay = 0.15f;
                if(applyFrictionOnJump) ApplyFriction(false);
                return;
            } else {
                if(doAirFriction && verticalAxis == 0 && horizontalAxis == 0) ApplyFriction(false);

                AirAccelerate();
            }
        }
        else if(grounded) {
            //if(velocity.y < 0) velocity.y = 0;

            //sliding
            if(crouching && new Vector3(velocity.x, 0f, velocity.z).magnitude >= minimumSlideSpeed) {
                //is sliding
                if(!sliding && slideTime >= slideDelay) { //can slide again
                    sliding = true;
                    slideTime = 0f;
                    DoSlide();
                }
            } else { //when we arent crouching and/or or speed becomes lower than min sliding speed (bc of friction)
                sliding = false;
            }

            cTime = 0;
            sprintAirDelay = 0;
            
            if(inputJumping && jumpTime >= jumpCoolDown) {
                Jump();
                sprintAirDelay = 0.15f;
                if(applyFrictionOnJump) ApplyFriction(false);
                return;
            } else {
                ApplyFriction(true);
            }

            Vector3 _wishDir = Vector3.zero;
            float _wishSpeed = 0f;
            float _accel = 0f;

            _wishDir = verticalAxis * bodyTransform.forward + horizontalAxis * bodyTransform.right;
            _wishDir.Normalize ();

            _wishSpeed = crouching ? crouchSpeed : ((inputSprinting && grounded && verticalAxis > 0) ? sprintSpeed : walkSpeed);

            _accel = (crouching ? crouchAcceleration : acceleration);

            Accelerate(_wishDir, _wishSpeed, _accel, false);
        }
        
        //clamp max velocity
        float yVel = velocity.y;
        velocity = Vector3.ClampMagnitude (new Vector3 (velocity.x, 0f, velocity.z), maxVelocity);
        velocity.y = yVel;
    }

    void HangleCrouch() {
        //crouch check
        if(grounded && crouching && !inputCrouching && Physics.BoxCast(targetPos + (1 - legHeight - 0.01f) * Vector3.up, legHalfExtent, transform.up, out RaycastHit headHit, Quaternion.identity, defaultHeight - currentHeight, groundLayer.value)) {
            //print("hit head");
            crouching = true;
        } else crouching = inputCrouching;

        


        prevCrouchOffset = crouchOffset;

        crouchingLerp = Mathf.Lerp(crouchingLerp, crouching ? 1f : 0f, Time.deltaTime * crouchingSpeed);

        currentHeight = Mathf.Lerp(defaultHeight, crouchingHeight, crouchingLerp);
        float realColliderHeight = currentHeight - legDistance;

        playerCollider.size = new Vector3(colliderSize * 2, realColliderHeight, colliderSize * 2);
        playerCollider.center = new Vector3(0, (2 - realColliderHeight) * 0.5f, 0);

        crouchOffset = (currentHeight - defaultHeight) * Vector3.up;

        if(grounded) {
            targetPos += crouchOffset - prevCrouchOffset;
            //velocity += (crouchOffset - prevCrouchOffset)/Time.fixedDeltaTime;
        } else {
            targetPos += (crouchOffset - prevCrouchOffset) * 0.333f;
            //velocity += (crouchOffset - prevCrouchOffset)* 0.333f/Time.fixedDeltaTime;
        }

    }

    void CheckGrounded() {
        if(Physics.BoxCast(targetPos + (1 - colliderSize) * transform.up, legHalfExtent, -transform.up, out hit, Quaternion.identity, currentHeight - colliderSize - legHeight, groundLayer.value)) {
            groundNormal = hit.normal;
            float groundSteepness = Vector3.Angle(Vector3.up, groundNormal);

            if(groundSteepness <= maxSlopeAngle) {
                grounded = true;
            } else grounded = false;
        } else {
            grounded = false;
        } 
    }

    void HangleGrounding() {
        //CheckGrounded();
        if(grounded) {
            //leg offset thing
            float disFromTarget = currentHeight - legHeight - colliderSize - hit.distance;

            float disToChange = disFromTarget * legSpeed * Time.deltaTime;
            
            if(disFromTarget > 0.01 && disFromTarget <= legDistance + 0.01f) {
                targetPos += disToChange * Vector3.up;
                //velocity += disToChange * Vector3.up/Time.fixedDeltaTime;
                //print("moving up: " + (disToChange * Vector3.up).ToString());
            } else if(disFromTarget > legDistance + 0.01f) {
                targetPos += (disFromTarget - legDistance - disToChange) * Vector3.up;
                //velocity += (disToChange - legDistance - disToChange)*Vector3.up/Time.fixedDeltaTime;
                //print("snaping up: " + (disFromTarget - legDistance - disToChange).ToString());
            } 
        }
    }

    void ApplyFriction(bool onGround) {
        Vector3 _vel = velocity;
        float _speed;
        float _newSpeed;
        float _control;
        float _drop;

        _vel.y = 0.0f;
        _speed = _vel.magnitude;

        float fric = crouching ? crouchFriction : friction;
        float decel = crouching ? crouchDeceleration : deceleration;

        if(sliding) fric = slideFriction;

        if(!onGround) fric = airFriction; //air friction

        // Only apply decel if the player is grounded
        if (onGround) _control = (_speed < decel) ? decel : _speed;
        else _control = _speed;

        //speed drops by it self * friction with time
        _drop = _control * fric * Time.deltaTime;

        _newSpeed = _speed - _drop;
        if(_newSpeed < 0) _newSpeed = 0;

        if (_newSpeed != _speed) {
            _newSpeed /= _speed; //idk man
        }

        // Set the end-velocity
        if(onGround) velocity *= _newSpeed;
        else {
            velocity.x *= _newSpeed;
            velocity.z *= _newSpeed;
        }
    }

    void AirAccelerate() {
        //find wishDir, wishSpeed, and Accel
        Vector3 _wishDir;
        Vector3 _wishVel;
        float _wishSpeed;
        float _accel;

        _wishVel = (verticalAxis * acceleration) * bodyTransform.forward + (horizontalAxis * acceleration) * bodyTransform.right;

        _wishSpeed = _wishVel.magnitude;
        _wishDir = _wishVel.normalized;

        // if (clampAirSpeed && (_wishSpeed != 0f && (_wishSpeed > maxSpeed))) {
        //     _wishVel = _wishVel * (maxSpeed / _wishSpeed);
        //     _wishSpeed = maxSpeed;
        // }

        _accel = airAcceleration;

        Accelerate(_wishDir, _wishSpeed, _accel, true);
    }

    void Accelerate(Vector3 wishDir, float wishSpeed, float acceleration, bool air) {

        float wishspd = wishSpeed;
        if(air) wishspd = Mathf.Min (wishspd, airCap);

        // Initialise variables
        float _addSpeed;
        float _accelerationSpeed;
        float _currentSpeed;
        
        // the sauce
        _currentSpeed = Vector3.Dot (velocity, wishDir);
        _addSpeed = wishspd - _currentSpeed;

        // If you're not actually increasing your speed, stop here.
        if (_addSpeed <= 0)
            return;

        // won't bother trying to understand any of this, really
        _accelerationSpeed = Mathf.Min (acceleration * wishSpeed * Time.deltaTime, _addSpeed);

        // Add the velocity to x and z (y handled by gravity and other things)
        velocity.x += _accelerationSpeed * wishDir.x;
        velocity.z += _accelerationSpeed * wishDir.z;

    }

    void Jump () {
        jumpTime = 0f;
        velocity.y = jumpForce;
        //might add stuff later so thats why its a function for just 1 line lmao, why am i saying lmao im actually schizo
    }

    //physics
    void DoCollisions (float rigidbodyPushForce) {

        // manual collision resolving
        int numOverlaps = 0;

        //capsule collider code
        // var capsule = playerCollider as CapsuleCollider;

        // Vector3 point1, point2;

        // var distanceToPoints = capsule.height / 2f - capsule.radius;
        // point1 = targetPos + capsule.center + Vector3.up * distanceToPoints;
        // point2 = targetPos + capsule.center - Vector3.up * distanceToPoints;

        // numOverlaps = Physics.OverlapCapsuleNonAlloc (point1, point2, capsule.radius, _colliders, groundLayer.value, QueryTriggerInteraction.Ignore);

        numOverlaps = Physics.OverlapBoxNonAlloc(playerCollider.center + targetPos, playerCollider.bounds.extents, _colliders,
            Quaternion.identity, groundLayer.value, QueryTriggerInteraction.Ignore);

        //print(numOverlaps);


        for (int i = 0; i < numOverlaps; i++) {

            Vector3 direction;
            float distance;

            if (Physics.ComputePenetration (playerCollider, targetPos,
                Quaternion.identity, _colliders [i], _colliders [i].transform.position,
                _colliders [i].transform.rotation, out direction, out distance)) {

                // Handle collision
                direction.Normalize ();
                Vector3 penetrationVector = direction * distance;
                Vector3 velocityProjected = Vector3.Project (velocity, -direction);
                //velocityProjected.y = 0; // don't touch y velocity, we need it to calculate fall damage elsewhere
                targetPos += penetrationVector;
                velocity -= velocityProjected;

                Rigidbody rb = _colliders [i].GetComponentInParent<Rigidbody> ();
                if (rb != null && !rb.isKinematic)
                    rb.AddForceAtPosition (velocityProjected * rigidbodyPushForce, targetPos, ForceMode.Impulse);

            }

        }

    }

    void DoSlide() {
        float slideSpeedCurrent = 0f;
        Vector3 slideDirection = Vector3.forward;
        Vector3 slideVel = Vector3.forward;

        slideVel = new Vector3 (velocity.x, 0f, velocity.z);

        slideDirection =  Vector3.Cross(groundNormal, Quaternion.AngleAxis (-90, Vector3.up) * slideVel.normalized);

        if(slideVel.magnitude >= maximumSlideSpeed) slideSpeedCurrent = slideVel.magnitude;
        else slideSpeedCurrent = Mathf.Min(slideVel.magnitude * slideSpeedMultiplier + addedSlideSpeed, maximumSlideSpeed);

        //some downhill thing idk man
        //slideSpeedCurrent -= (slideDirection * slideSpeedCurrent).y * downhillSlideSpeedMultiplier * Time.deltaTime; 

        slideVel = slideDirection * slideSpeedCurrent;

        velocity.x = slideVel.x;
        velocity.z = slideVel.z;

        //fov change for paul game, bad code but just a quick fix
        // if(GetComponent<Shooting>() != null) {
        //     GetComponent<Shooting>().targetSlideFov += GetComponent<Shooting>().slideFov;
        // }
    } 

    public void Die() {
        Vector3 spawnPos = transform.position + new Vector3(0, 10, 0);
        targetPos = spawnPos;
        velocity = Vector3.zero;
        transform.position = spawnPos;
        //rb.MovePosition(spawnPos);
    }
}