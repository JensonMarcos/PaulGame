using System;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using TMPro;
using UnityEngine;

public struct CharacterInputs
{
    public float ForwardAxis;
    public float RightAxis;
    public Quaternion CameraRotation;
    public bool Jump;
    public bool Crouch;
    public bool Sprint;
}

public enum Stance : byte
{
    Stand, Crouch, Slide, Sprint
}

[System.Serializable]
public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor;

    public Transform camTarget;
    public Transform root;

    CharacterInputs characterInputs;
    Vector3 wishMovement;
    bool wishJump;
    bool wishCrouch;
    bool wishCrouchInAir;
    bool wishSprint;

    float timeUngrounded;
    float timeJumpRequested;
    bool ungroundedBcJump;

    Collider[] uncrouchColliders = new Collider[8];

    public CharacterState State;
    CharacterState lastState, tempState;
    
    [SerializeField] LayerMask playerLayerMask, spectatorLayerMask;
    bool spectator;

    [Space]
    [Header("Walk")]
    public float walkSpeed;
    [SerializeField] float walkAcceleration;

    [Space]
    [Header("Sprint")]
    public float sprintSpeed;
    [SerializeField] float sprintAcceleration;

    [Space]
    [Header("Air")]
    //[SerializeField] float airSpeed;
    //[SerializeField] float airSpeedCap;
    [SerializeField] float airAcceleration;
    [SerializeField] float lurchForce;
    [SerializeField] float lurchThreshold;

    [Space]
    [Header("Crouch")]
    public float crouchSpeed;
    [SerializeField] float crouchAcceleration;
    
    public float standHeight, crouchHeight;
    [SerializeField] float camStandHeight, camCrouchHeight;

    [Space]
    [Header("Jump")]
    [SerializeField] float jumpSpeed;
    [SerializeField] float coyoteTime;
    [SerializeField] float gravity;

    [Space]
    [Header("Slide")]
    [SerializeField] float slideStartSpeed;
    [SerializeField] float slideEndSpeed;
    [SerializeField] float slideThresholdSpeed;
    [SerializeField] float slideFriction;
    [SerializeField] float slideAcceleration;

    [Space]
    [Header("Wall Jump")]
    [SerializeField] float wallJumpSpeed;
    [SerializeField] float wallJumpPushSpeed;
    [SerializeField] float wallCheckDistance;

    [Space]
    [Header("Vault")]
    [SerializeField] float vaultUpSpeed;
    [SerializeField] float vaultCheckHeight;

    Vector3 wallNormal, lastWallJumpNormal;
    bool canWallJump;
    bool canVault;
    RaycastHit[] wallHits = new RaycastHit[8];

    float lurchTimer;
    bool movedLastFrame;

    [SerializeField] TMP_Text speedText;

    public void Initialize()
    {
        Motor.CharacterController = this;
        Motor.SetCapsuleDimensions(Motor.Capsule.radius, standHeight, standHeight * 0.5f);
        State.Stance = Stance.Stand;
        lastState = State;

        uncrouchColliders = new Collider[8];
    }


    public void SetInputs(CharacterInputs inputs)
    {
        characterInputs = inputs;

        wishMovement = Vector3.ClampMagnitude(new Vector3(characterInputs.RightAxis, 0f, characterInputs.ForwardAxis), 1f);
        wishMovement = transform.rotation * wishMovement;

        var wasRequestingJump = wishJump;
        wishJump = wishJump || characterInputs.Jump;
        if (wishJump && !wasRequestingJump) timeJumpRequested = 0f;

        var wasRequestingCrouch = wishCrouch;
        wishCrouch = characterInputs.Crouch;
        if (wishCrouch && !wasRequestingCrouch)
            wishCrouchInAir = !State.Grounded;
        else if (!wishCrouch && wasRequestingCrouch)
            wishCrouchInAir = false;

        wishSprint = characterInputs.Sprint;
        if (wishCrouch || !State.Grounded || State.Velocity.sqrMagnitude < 0.01) wishSprint = false;

        if(spectator)
        {
            wishCrouch = false;
            wishSprint = false;
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its rotation should be right now. 
    /// This is the ONLY place where you should set the character's rotation
    /// </summary>
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        var forward = Vector3.ProjectOnPlane(characterInputs.CameraRotation * Vector3.forward, Motor.CharacterUp);

        if (forward != Vector3.zero) currentRotation = Quaternion.LookRotation(forward, Motor.CharacterUp);
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its velocity should be right now. 
    /// This is the ONLY place where you can set the character's velocity
    /// </summary>
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        //grounded
        if (Motor.GroundingStatus.IsStableOnGround && !wishJump)
        {
            timeUngrounded = 0f;
            ungroundedBcJump = false;

            var groundedMovement = Motor.GetDirectionTangentToSurface(wishMovement, Motor.GroundingStatus.GroundNormal);
            //initiate slide
            {
                //var moving = groundedMovement.sqrMagnitude > 0f;
                var crouching = State.Stance == Stance.Crouch;
                var wasStanding = lastState.Stance is Stance.Stand or Stance.Sprint;
                var wasInAir = !lastState.Grounded;

                if (crouching && (wasStanding || wasInAir) && Motor.Velocity.magnitude >= slideThresholdSpeed)
                {
                    State.Stance = Stance.Slide;

                    if (wasInAir)
                    {
                        currentVelocity = Vector3.ProjectOnPlane(lastState.Velocity, Motor.GroundingStatus.GroundNormal);
                    }

                    var effectiveSlideStartSpeed = slideStartSpeed; //some bs, idk
                    if (!lastState.Grounded && !wishCrouchInAir)
                    {
                        effectiveSlideStartSpeed = 0f;
                        wishCrouchInAir = false;
                    }
                    var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) * slideSpeed;
                }
            }
            //move
            if (State.Stance is Stance.Stand or Stance.Crouch or Stance.Sprint)
            {
                if (State.Stance is Stance.Stand or Stance.Sprint) State.Stance = wishSprint ? Stance.Sprint : Stance.Stand;

                var speed = State.Stance switch
                {
                    Stance.Stand => walkSpeed,
                    Stance.Sprint => sprintSpeed,
                    Stance.Crouch => crouchSpeed,
                    Stance.Slide => 0f,
                    _ => 0f,
                };

                var acceleration = State.Stance switch
                {
                    Stance.Stand => walkAcceleration,
                    Stance.Sprint => sprintAcceleration,
                    Stance.Crouch => crouchAcceleration,
                    Stance.Slide => 0f,
                    _ => 0f,
                };

                var targetVelocity = groundedMovement * speed;
                currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-acceleration * deltaTime));
                //currentVelocity *= Friction(currentVelocity, walkFriction, walkDeceleration);
                //currentVelocity += Accelerate(groundedMovement, speed, acceleration, currentVelocity);
            }
            else //sliding
            {
                currentVelocity -= currentVelocity * slideFriction * deltaTime;

                //slope
                var force = Vector3.ProjectOnPlane(-Motor.CharacterUp, Motor.GroundingStatus.GroundNormal) * gravity;
                currentVelocity -= force * deltaTime;

                //steer
                if(wishMovement.sqrMagnitude > 0f)
                {
                    // var currentSpeed = currentVelocity.magnitude;
                    // var targetVelocity = groundedMovement * currentVelocity.magnitude;
                    // var steerForce = (targetVelocity - currentVelocity) * slideAcceleration * deltaTime;
                    // currentVelocity += steerForce;
                    // currentVelocity = Vector3.ClampMagnitude(currentVelocity, currentSpeed);

                    // var wishDir = Vector3.ProjectOnPlane(wishMovement, Motor.CharacterUp).normalized;
                    // var planarVel = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                    var currentSpeed = Vector3.Dot(currentVelocity, groundedMovement);
                    var addSpeed = slideAcceleration - currentSpeed;

                    if (addSpeed > 0f) currentVelocity += groundedMovement * addSpeed;
                }
                
                if (currentVelocity.magnitude < slideEndSpeed) State.Stance = Stance.Crouch;
            }
        }
        else //air
        {
            if (State.Stance == Stance.Sprint) State.Stance = Stance.Stand;
            timeUngrounded += deltaTime;
            lurchTimer += deltaTime;

            if (wishMovement.sqrMagnitude > 0f)
            {
                if(lurchTimer < lurchThreshold && !movedLastFrame)
                {
                    lurchTimer += lurchThreshold;

                    var velBefore = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
                    // if(velBefore.magnitude < lurchForce) velBefore = velBefore.normalized * lurchForce;
                    var addForce = wishMovement * lurchForce;
                    // if((velBefore + addForce).magnitude > velBefore.magnitude && velBefore.magnitude > lurchForce)
                    // {
                    //     addForce = (velBefore + addForce).normalized * velBefore.magnitude - velBefore;
                        
                    // }
                    // currentVelocity += addForce;
                    if(velBefore.magnitude < lurchForce) 
                        currentVelocity = Vector3.ClampMagnitude(currentVelocity + addForce, lurchForce);
                    else
                        currentVelocity = Vector3.ClampMagnitude(currentVelocity + addForce, velBefore.magnitude);
                }


                /* //NORMAL AIR MOVEMENT

                var planarMovement = Vector3.ProjectOnPlane(wishMovement, Motor.CharacterUp);
                var currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
                var movementForce = planarMovement * airAcceleration * deltaTime;
                //var movementForce = Accelerate(planarMovement, airSpeed, airAcceleration, currentPlanarVelocity);

                if (currentPlanarVelocity.magnitude < airSpeed) //add air movement when below max speed
                {
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                    movementForce = targetPlanarVelocity - currentPlanarVelocity;
                }
                else if (Vector3.Dot(currentPlanarVelocity, movementForce) > 0f)
                { //add movement force that isnt toward velocity
                    var constrainedMovementForce = Vector3.ProjectOnPlane(movementForce, currentPlanarVelocity.normalized);
                    movementForce = constrainedMovementForce;
                }

                currentVelocity += movementForce;
                */
                
                //AIR STRAFING 

                var planarVel = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                //var wishSpeed = airSpeed;
                //var cappedWishSpeed = Mathf.Min(wishSpeed, airSpeedCap);

                var currentSpeed = Vector3.Dot(planarVel, wishMovement);
                var addSpeed = airAcceleration - currentSpeed; //using airAccel as the airSpeedCap, simplified

                if (addSpeed > 0f)
                {
                    // var accelSpeed = Mathf.Min(airAcceleration * wishSpeed * deltaTime, addSpeed);
                    // currentVelocity += wishDir * accelSpeed;

                    //lowkey feels better just adding the full speed
                    currentVelocity += wishMovement * addSpeed;
                }

            }
    
            currentVelocity += Motor.CharacterUp * gravity * deltaTime;
        }
        movedLastFrame = wishMovement.magnitude > 0.1f;

        if (wishJump)
        {
            var grounded = Motor.GroundingStatus.IsStableOnGround;
            var canCoyote = timeUngrounded < coyoteTime && !ungroundedBcJump;

            if (grounded || canCoyote)
            {
                wishJump = false;
                lurchTimer = 0f;

                Motor.ForceUnground(0.1f);
                ungroundedBcJump = true;

                var currentVerticalSpeed = Vector3.Dot(currentVelocity, Motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += Motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
            else if (canVault)
            {
                wishJump = false;

                currentVelocity = Motor.CharacterUp * vaultUpSpeed;
            }
            else if (canWallJump)
            {
                wishJump = false;
                lurchTimer = 0f;

                lastWallJumpNormal = wallNormal;

                var currentVerticalSpeed = Vector3.Dot(currentVelocity, Motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, wallJumpSpeed);
                currentVelocity += Motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);

                var currentWallSpeed = Vector3.Dot(currentVelocity, wallNormal);
                var targetWallSpeed = Mathf.Max(currentWallSpeed, wallJumpPushSpeed);
                currentVelocity += wallNormal * (targetWallSpeed - currentWallSpeed);
            }
            else
            {
                timeJumpRequested += deltaTime;

                var canJumpLater = timeJumpRequested < coyoteTime;

                wishJump = canJumpLater;
            }
        }


        speedText.text = $"Speed: {Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp).magnitude:0.00} m/s, State: {State.Stance}";
    }


    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called before the character begins its movement update
    /// </summary>
    public void BeforeCharacterUpdate(float deltaTime)
    {
        tempState = State;
        if (wishCrouch && State.Stance is Stance.Stand or Stance.Sprint)
        {
            State.Stance = Stance.Crouch;
            Motor.SetCapsuleDimensions(Motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
            //camTarget.localPosition = new Vector3(0, camCrouchHeight, 0);
            //root.localScale = new Vector3(1, crouchHeight / standHeight, 1);
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called after the character has finished its movement update
    /// </summary>
    public void AfterCharacterUpdate(float deltaTime)
    {
        if (!wishCrouch && State.Stance is not (Stance.Stand or Stance.Sprint))
        {
            Motor.SetCapsuleDimensions(Motor.Capsule.radius, standHeight, standHeight * 0.5f);

            //check if can uncrouch
            if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, uncrouchColliders, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
            {
                Motor.SetCapsuleDimensions(Motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
            }
            else
            {
                State.Stance = wishSprint ? Stance.Sprint : Stance.Stand;
                //camTarget.localPosition = new Vector3(0, camStandHeight, 0);
                //root.localScale = Vector3.one;
            }

        }

        UpdateWallContact();

        State.Grounded = Motor.GroundingStatus.IsStableOnGround;
        State.Velocity = Motor.Velocity;
        lastState = tempState;
    }

    void UpdateWallContact()
    {
        canWallJump = false;
        canVault = false;

        if (Motor.GroundingStatus.IsStableOnGround)
        {
            lastWallJumpNormal = Vector3.zero;
            wallNormal = Vector3.zero;
            return;
        }

        if (wallNormal == Vector3.zero) return; //hasnt been seeded (didnt hit a wall)

        if (Motor.CharacterCollisionsSweep(Motor.TransientPosition, Motor.TransientRotation, -wallNormal, wallCheckDistance, out RaycastHit hit, wallHits) > 0 && Mathf.Abs(Vector3.Dot(hit.normal, Motor.CharacterUp)) < 0.1f)
        {
            wallNormal = hit.normal;

            if(wallNormal != lastWallJumpNormal)
            {
                canWallJump = true;
            }

            if(Vector3.Dot(transform.forward, wallNormal) < -0.5f && Motor.CharacterCollisionsRaycast(Motor.TransientPosition + Motor.CharacterUp * vaultCheckHeight, -wallNormal, Motor.Capsule.radius + wallCheckDistance, out _, wallHits) == 0)
            {
                canVault = true;
            }
        }
        else
        {
            wallNormal = Vector3.zero;
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (!Motor.GroundingStatus.IsStableOnGround && State.Stance is Stance.Slide) State.Stance = Stance.Crouch;
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        //seed the wall direction
        if (Motor.GroundingStatus.IsStableOnGround) return;

        if (Mathf.Abs(Vector3.Dot(hitNormal, Motor.CharacterUp)) > 0.1f) return;

        wallNormal = hitNormal;
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        Motor.SetPosition(position);
        if (killVelocity) Motor.BaseVelocity = Vector3.zero;
    }

    public void SetSpectator(bool _spectator)
    {
        spectator = _spectator;
        Motor.CollidableLayers = _spectator ? spectatorLayerMask : playerLayerMask;
    }

    public void AddForce(Vector3 force)
    {
        Motor.ForceUnground();
        Motor.BaseVelocity += force;
    }
}