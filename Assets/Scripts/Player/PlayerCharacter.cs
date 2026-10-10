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
    Stand, Crouch, Slide, Sprint, Vault
}

//everything that carries over from one tick to the next lives here (plus the motor's own Motor.GetState())
[System.Serializable]
public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;

    [Header("Last Tick")]
    public bool LastGrounded;
    public Stance LastStance;
    public Vector3 LastVelocity;

    [Header("Wish")]
    public bool WishJump;
    public bool WishCrouch;
    public bool WishCrouchInAir;
    public float TimeJumpRequested;

    [Header("Air")]
    public float TimeUngrounded;
    public bool UngroundedBcJump;
    public float LurchTimer;
    public bool MovedLastTick;

    [Header("Wall Jump")]
    public Vector3 LastWallJumpNormal;
    public int ConsecutiveWallJumps;
    public float WallJumpCooldownRemaining;

    [Header("Vault")]
    public bool Vaulting;
    public Vector3 VaultForwardDir;
    public float VaultUpRemaining;

    [Header("Events")]
    public byte WallJumps;
    public Vector3 HandPush;
    public byte Teleports;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    public KinematicCharacterMotor Motor;

    public Transform camTarget;
    public Transform root;

    Player player;

    CharacterInputs characterInputs;
    Vector3 wishMovement;
    bool wishSprint;

    Collider[] uncrouchColliders = new Collider[8];

    public CharacterState State;
    CharacterState tempState;
    
    [SerializeField] LayerMask playerLayerMask, spectatorLayerMask;
    bool spectator;

    int tick;
    float tickDeltaTime;
    Vector3 pendingForce;
    bool pendingTeleport;
    bool pendingKillVelocity;
    Vector3 pendingTeleportPosition;

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
    [SerializeField] float airDeceleration;
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
    [SerializeField] float slideDeceleration;

    [Space]
    [Header("Wall Jump")]
    [SerializeField] float wallJumpSpeed;
    [SerializeField] float wallJumpPushSpeed;
    [SerializeField] float wallCheckDistance;
    [SerializeField] float handPushAmount;
    [SerializeField] float wallJumpCooldown = 1f;

    [Space]
    [Header("Vault")]
    [SerializeField] float vaultForwardSpeed;
    [SerializeField] float vaultExtraHeight;
    [SerializeField] float vaultCheckHeight;

    Vector3 wallNormal;
    bool canWallJump;
    bool canVault;
    RaycastHit[] wallHits = new RaycastHit[8];

    Vector3 vaultLandingPos;

    [SerializeField] TMP_Text speedText;

    [Space]
    [Header("Prop Kicking")]
    [SerializeField] float kickForce = 1f;
    [SerializeField] float minKickSpeed = 1f;
    [SerializeField] float kickInterval = 0.1f;

    PredictedProp lastKickedProp;
    int lastKickTick;
    PredictedProp pendingKickProp;
    Vector3 pendingKickForce, pendingKickPoint;

    public void Initialize()
    {
        Motor.CharacterController = this;
        Motor.SetCapsuleDimensions(Motor.Capsule.radius, standHeight, standHeight * 0.5f);
        Motor.SetPositionAndRotation(transform.position, transform.rotation);
        State.Stance = Stance.Stand;
        player = transform.root.GetComponent<Player>();

        uncrouchColliders = new Collider[8];
    }

    public void Tick(CharacterInputs _inputs, int _tick, float _deltaTime)
    {
        tick = _tick;
        tickDeltaTime = _deltaTime;

        //teleports and forces that came in between ticks
        if (pendingTeleport)
        {
            pendingTeleport = false;
            Motor.SetPositionAndRotation(GetValidPosition(pendingTeleportPosition), Motor.TransientRotation);
            if (pendingKillVelocity) Motor.BaseVelocity = Vector3.zero;
            State.Teleports++;
        }
        if (pendingForce != Vector3.zero)
        {
            Motor.ForceUnground();
            Motor.BaseVelocity += pendingForce;
            pendingForce = Vector3.zero;
        }

        SetInputs(_inputs);

        //only the local player's motor is enabled (Player disables remote ones), so this only moves this character
        KinematicCharacterSystem.PreSimulationInterpolationUpdate(_deltaTime);
        KinematicCharacterSystem.Simulate(_deltaTime, KinematicCharacterSystem.CharacterMotors, KinematicCharacterSystem.PhysicsMovers);
        KinematicCharacterSystem.PostSimulationInterpolationUpdate(_deltaTime);

        if (pendingKickProp != null)
        {
            pendingKickProp.ApplyForceServerRpc(pendingKickForce, pendingKickPoint);
            pendingKickProp = null;
        }
    }

    void SetInputs(CharacterInputs _inputs)
    {
        characterInputs = _inputs;

        wishMovement = Vector3.ClampMagnitude(new Vector3(characterInputs.RightAxis, 0f, characterInputs.ForwardAxis), 1f);
        wishMovement = Motor.TransientRotation * wishMovement;

        bool _wasRequestingJump = State.WishJump;
        State.WishJump = State.WishJump || characterInputs.Jump;
        if (State.WishJump && !_wasRequestingJump) State.TimeJumpRequested = 0f;

        bool _wasRequestingCrouch = State.WishCrouch;
        State.WishCrouch = characterInputs.Crouch;
        if (State.WishCrouch && !_wasRequestingCrouch)
            State.WishCrouchInAir = !State.Grounded;
        else if (!State.WishCrouch && _wasRequestingCrouch)
            State.WishCrouchInAir = false;

        wishSprint = characterInputs.Sprint;
        if (State.WishCrouch || !State.Grounded || State.Velocity.sqrMagnitude < 0.01) wishSprint = false;

        if(spectator)
        {
            State.WishCrouch = false;
            //wishSprint = false;
        }
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its rotation should be right now. 
    /// This is the ONLY place where you should set the character's rotation
    /// </summary>
    public void UpdateRotation(ref Quaternion _currentRotation, float _deltaTime)
    {
        Vector3 _forward = Vector3.ProjectOnPlane(characterInputs.CameraRotation * Vector3.forward, Motor.CharacterUp);

        if (_forward != Vector3.zero) _currentRotation = Quaternion.LookRotation(_forward, Motor.CharacterUp);
    }

    public void WriteState(ref PlayerState _state)
    {
        _state.Position = Motor.TransientPosition;
        _state.Grounded = State.Grounded;
        _state.Stance = State.Stance;
        _state.Velocity = State.Velocity;
        _state.WallJumps = State.WallJumps;
        _state.HandPush = State.HandPush;
        _state.Teleports = State.Teleports;
    }

    public void SetYawFromCamera(Quaternion _cameraRotation)
    {
        Vector3 _forward = Vector3.ProjectOnPlane(_cameraRotation * Vector3.forward, Motor.CharacterUp);
        if (_forward != Vector3.zero) transform.rotation = Quaternion.LookRotation(_forward, Motor.CharacterUp);
    }

    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is where you tell your character what its velocity should be right now. 
    /// This is the ONLY place where you can set the character's velocity
    /// </summary>
    public void UpdateVelocity(ref Vector3 _currentVelocity, float _deltaTime)
    {
        //grounded
        if (Motor.GroundingStatus.IsStableOnGround && !State.WishJump)
        {
            State.TimeUngrounded = 0f;
            State.UngroundedBcJump = false;

            Vector3 _groundedMovement = Motor.GetDirectionTangentToSurface(wishMovement, Motor.GroundingStatus.GroundNormal);
            //initiate slide
            {
                //var moving = groundedMovement.sqrMagnitude > 0f;
                bool _crouching = State.Stance == Stance.Crouch;
                bool _wasStanding = State.LastStance is Stance.Stand or Stance.Sprint;
                bool _wasInAir = !State.LastGrounded;

                if (_crouching && (_wasStanding || _wasInAir) && Motor.Velocity.magnitude >= slideThresholdSpeed)
                {
                    State.Stance = Stance.Slide;

                    if (_wasInAir)
                    {
                        _currentVelocity = Vector3.ProjectOnPlane(State.LastVelocity, Motor.GroundingStatus.GroundNormal);
                    }

                    float _effectiveSlideStartSpeed = slideStartSpeed; //some bs, idk
                    if (!State.LastGrounded && !State.WishCrouchInAir)
                    {
                        _effectiveSlideStartSpeed = 0f;
                        State.WishCrouchInAir = false;
                    }
                    float _slideSpeed = Mathf.Max(_effectiveSlideStartSpeed, _currentVelocity.magnitude);
                    _currentVelocity = Motor.GetDirectionTangentToSurface(_currentVelocity, Motor.GroundingStatus.GroundNormal) * _slideSpeed;
                }
            }
            //move
            if (State.Stance is not Stance.Slide)
            {
                if (State.Stance is Stance.Stand or Stance.Sprint) State.Stance = wishSprint ? Stance.Sprint : Stance.Stand;

                float _speed = State.Stance switch
                {
                    Stance.Stand => walkSpeed,
                    Stance.Sprint => sprintSpeed,
                    Stance.Crouch => crouchSpeed,
                    Stance.Slide => 0f,
                    Stance.Vault => 0f,
                    _ => 0f,
                };

                float _acceleration = State.Stance switch
                {
                    Stance.Stand => walkAcceleration,
                    Stance.Sprint => sprintAcceleration,
                    Stance.Crouch => crouchAcceleration,
                    Stance.Slide => 0f,
                    Stance.Vault => 0f,
                    _ => 0f,
                };

                Vector3 _targetVelocity = _groundedMovement * _speed;
                _currentVelocity = Vector3.Lerp(_currentVelocity, _targetVelocity, 1f - Mathf.Exp(-_acceleration * _deltaTime));
                //currentVelocity *= Friction(currentVelocity, walkFriction, walkDeceleration);
                //currentVelocity += Accelerate(groundedMovement, speed, acceleration, currentVelocity);
            }
            else //sliding
            {
                _currentVelocity -= _currentVelocity * slideFriction * _deltaTime;

                //slope
                Vector3 _force = Vector3.ProjectOnPlane(-Motor.CharacterUp, Motor.GroundingStatus.GroundNormal) * gravity;
                _currentVelocity -= _force * _deltaTime;

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

                    float _currentSpeed = Vector3.Dot(_currentVelocity, _groundedMovement);
                    float _addSpeed = slideAcceleration - _currentSpeed;

                    if (_addSpeed > 0f)
                    {
                        float _accelSpeed = _addSpeed;
                        float _slideSpeed = _currentVelocity.magnitude;
                        if (_currentSpeed < 0f && _slideSpeed > 0.01f)
                        {
                            float _oppose = Mathf.Clamp01(-Vector3.Dot(_currentVelocity / _slideSpeed, _groundedMovement.normalized));
                            _oppose *= _oppose;
                            _accelSpeed = Mathf.Lerp(_addSpeed, Mathf.Min(_addSpeed, slideDeceleration * _deltaTime), _oppose);
                        }

                        _currentVelocity += _groundedMovement * _accelSpeed;
                    }
                }
                
                if (_currentVelocity.magnitude < slideEndSpeed) State.Stance = Stance.Crouch;
            }
        }
        else //air
        {
            if (State.Stance == Stance.Sprint) State.Stance = Stance.Stand;
            State.TimeUngrounded += _deltaTime;
            State.LurchTimer += _deltaTime;

            if (wishMovement.sqrMagnitude > 0f)
            {
                if(State.LurchTimer < lurchThreshold && !State.MovedLastTick)
                {
                    State.LurchTimer += lurchThreshold;
                    
                    float _yvel = Vector3.Dot(_currentVelocity, Motor.CharacterUp);
                    Vector3 _velBefore = Vector3.ProjectOnPlane(_currentVelocity, Motor.CharacterUp);
                    Vector3 _addForce = wishMovement * lurchForce;
                    float _planarSpeed = _velBefore.magnitude;
                    if (_planarSpeed > 0.01f)
                    {
                        Vector3 _velDir = _velBefore / _planarSpeed;
                        float _along = Vector3.Dot(_addForce, _velDir);
                        if (_along < 0f)
                            _addForce -= _velDir * _along * Mathf.Clamp01(_planarSpeed / lurchForce);
                    }
                    if(_planarSpeed < lurchForce) 
                        _currentVelocity = Vector3.ClampMagnitude(_velBefore + _addForce, lurchForce) + Motor.CharacterUp * _yvel;
                    else
                        _currentVelocity = Vector3.ClampMagnitude(_velBefore + _addForce, _planarSpeed) + Motor.CharacterUp * _yvel;
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

                Vector3 _planarVel = Vector3.ProjectOnPlane(_currentVelocity, Motor.CharacterUp);

                //var wishSpeed = airSpeed;
                //var cappedWishSpeed = Mathf.Min(wishSpeed, airSpeedCap);

                float _currentSpeed = Vector3.Dot(_planarVel, wishMovement);
                float _addSpeed = airAcceleration - _currentSpeed; //using airAccel as the airSpeedCap, simplified

                if (_addSpeed > 0f)
                {
                    float _accelSpeed = _addSpeed;
                    float _planarSpeed = _planarVel.magnitude;
                    if (_currentSpeed < 0f && _planarSpeed > 0.01f)
                    {
                        // 0 at 90°, 1 at 180°. Squared so typical air-strafe angles keep the instant bite.
                        float _oppose = Mathf.Clamp01(-Vector3.Dot(_planarVel / _planarSpeed, wishMovement.normalized));
                        _oppose *= _oppose;
                        _accelSpeed = Mathf.Lerp(_addSpeed, Mathf.Min(_addSpeed, airDeceleration * _deltaTime), _oppose);
                    }

                    _currentVelocity += wishMovement * _accelSpeed;
                }

            }
    
            _currentVelocity += Motor.CharacterUp * gravity * _deltaTime;
        }
        State.MovedLastTick = wishMovement.magnitude > 0.1f;

        if (State.Vaulting) {
            if (State.VaultUpRemaining > 0f)
            {
                State.VaultUpRemaining -= _deltaTime;
            } else {
                _currentVelocity += State.VaultForwardDir * vaultForwardSpeed;
                State.Vaulting = false;
                State.Stance = Stance.Stand;
            }
        }

        if (State.WishJump)
        {
            //probe for walls
            UpdateWallContact();

            bool _grounded = Motor.GroundingStatus.IsStableOnGround;
            bool _canCoyote = State.TimeUngrounded < coyoteTime && !State.UngroundedBcJump;

            if (canVault)
            {
                State.WishJump = false;
                State.LurchTimer = 0f;

                StartVault(ref _currentVelocity);

                State.Stance = Stance.Vault;
            }
            else if (_grounded || _canCoyote)
            {
                State.WishJump = false;
                State.LurchTimer = 0f;

                Motor.ForceUnground(0.1f);
                State.UngroundedBcJump = true;

                float _currentVerticalSpeed = Vector3.Dot(_currentVelocity, Motor.CharacterUp);
                float _targetVerticalSpeed = Mathf.Max(_currentVerticalSpeed, jumpSpeed);
                _currentVelocity += Motor.CharacterUp * (_targetVerticalSpeed - _currentVerticalSpeed);
            }
            else if (canWallJump && State.WallJumpCooldownRemaining <= 0f)
            {
                State.WishJump = false;
                State.LurchTimer = 0f;

                State.LastWallJumpNormal = wallNormal;

                float _currentVerticalSpeed = Vector3.Dot(_currentVelocity, Motor.CharacterUp);
                float _targetVerticalSpeed = Mathf.Max(_currentVerticalSpeed, wallJumpSpeed);
                _currentVelocity += Motor.CharacterUp * (_targetVerticalSpeed - _currentVerticalSpeed);

                float _currentWallSpeed = Vector3.Dot(_currentVelocity, wallNormal);
                float _targetWallSpeed = Mathf.Max(_currentWallSpeed, wallJumpPushSpeed);
                _currentVelocity += wallNormal * (_targetWallSpeed - _currentWallSpeed);

                State.HandPush = transform.InverseTransformDirection(-wallNormal) * handPushAmount;
                State.WallJumps++;

                State.ConsecutiveWallJumps++;
                if (State.ConsecutiveWallJumps >= 2)
                {
                    State.WallJumpCooldownRemaining = wallJumpCooldown;
                    State.ConsecutiveWallJumps = 0;
                }
            }
            else
            {
                State.TimeJumpRequested += _deltaTime;

                bool _canJumpLater = State.TimeJumpRequested < coyoteTime;

                State.WishJump = _canJumpLater;
            }
        }


        //speedText.text = $"Speed: {Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp).magnitude:0.00} m/s, State: {State.Stance}";
    }


    /// <summary>
    /// (Called by KinematicCharacterMotor during its update cycle)
    /// This is called before the character begins its movement update
    /// </summary>
    public void BeforeCharacterUpdate(float _deltaTime)
    {
        tempState = State;
        if (State.WishCrouch && State.Stance is Stance.Stand or Stance.Sprint)
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
    public void AfterCharacterUpdate(float _deltaTime)
    {
        if (!State.WishCrouch && State.Stance is not (Stance.Stand or Stance.Sprint or Stance.Vault))
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

        if (State.WallJumpCooldownRemaining > 0f) State.WallJumpCooldownRemaining -= _deltaTime;

        if (Motor.GroundingStatus.IsStableOnGround)
        {
            State.LastWallJumpNormal = Vector3.zero;
            State.ConsecutiveWallJumps = 0;
        }


        State.Grounded = Motor.GroundingStatus.IsStableOnGround;
        State.Velocity = Motor.Velocity;
        State.LastGrounded = tempState.Grounded;
        State.LastStance = tempState.Stance;
        State.LastVelocity = tempState.Velocity;
    }

    void UpdateWallContact()
    {
        canWallJump = false;
        canVault = false;
        wallNormal = Vector3.zero;

        //fan sweeps out around the player to find a wall (the capsule radius makes each sweep a wide swath, so cardinal directions cover all sides)
        const int _directionCount = 4;
        for (int i = 0; i < _directionCount; i++)
        {
            Vector3 _direction = Quaternion.AngleAxis(i * (360f / _directionCount), Motor.CharacterUp) * transform.forward;

            if (Motor.CharacterCollisionsSweep(Motor.TransientPosition, Motor.TransientRotation, _direction, wallCheckDistance, out RaycastHit _hit, wallHits) == 0) continue;

            //only near-vertical walls, ignore floors/ceilings
            if (Mathf.Abs(Vector3.Dot(_hit.normal, Motor.CharacterUp)) >= 0.1f) continue;

            Vector3 _normal = _hit.normal;

            //wall jump off any wall we didnt just jump from
            if (!canWallJump && _normal != State.LastWallJumpNormal)
            {
                canWallJump = true;
                wallNormal = _normal;
            }

            //vault over a wall we are facing if theres a valid surface to land on
            if (Vector3.Dot(transform.forward, _normal) < -0.5f && TryFindVaultLanding(_normal, out Vector3 _landing))
            {
                canVault = true;
                wallNormal = _normal;
                vaultLandingPos = _landing;
            }
        }
    }

    bool TryFindVaultLanding(Vector3 _wallNormalDir, out Vector3 _landing)
    {
        _landing = Vector3.zero;

        Vector3 _up = Motor.CharacterUp;
        Vector3 _into = Vector3.ProjectOnPlane(-_wallNormalDir, _up).normalized;
        float _radius = Motor.Capsule.radius;

        //the wall must be low enough that theres clearance to pass over it
        if (Motor.CharacterCollisionsRaycast(Motor.TransientPosition + _up * vaultCheckHeight, _into, _radius + wallCheckDistance, out _, wallHits) > 0)
            return false;

        //just past the edge so the feet land on stable ground without going deep onto the platform
        float _forwardDist = _radius + wallCheckDistance + 0.1f;
        Vector3 _probeTop = Motor.TransientPosition + _up * vaultCheckHeight + _into * _forwardDist;

        //look down for the platform surface beneath the probe point
        if (Motor.CharacterCollisionsRaycast(_probeTop, -_up, vaultCheckHeight + 0.2f, out RaycastHit _hit, wallHits) == 0)
            return false;

        //only vault onto near-flat ground we can actually stand on
        if (Vector3.Dot(_hit.normal, _up) < 0.7f) return false;

        _landing = _hit.point;

        //make sure the character fits at the landing spot
        if (Motor.CharacterOverlap(_landing + _up * 0.05f, Motor.TransientRotation, uncrouchColliders, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
            return false;

        return true;
    }

    void StartVault(ref Vector3 _currentVelocity)
    {
        State.Vaulting = true;
        State.UngroundedBcJump = true;
        Motor.ForceUnground(0.1f);

        Vector3 _up = Motor.CharacterUp;
        Vector3 _toTarget = vaultLandingPos - Motor.TransientPosition;
        Vector3 _horizontal = Vector3.ProjectOnPlane(_toTarget, _up);

        State.VaultForwardDir = _horizontal.sqrMagnitude > 0f ? _horizontal.normalized : Vector3.ProjectOnPlane(transform.forward, _up).normalized;

        //cancel sideways (left/right) velocity so the vault goes straight over the wall
        Vector3 _lateral = Vector3.Cross(_up, State.VaultForwardDir).normalized;
        _currentVelocity -= _lateral * Vector3.Dot(_currentVelocity, _lateral);

        //rise this far before the forward force kicks in (the actual ledge height)
        float _climbHeight = Mathf.Max(0f, Vector3.Dot(_toTarget, _up));

        State.VaultUpRemaining = Mathf.Sqrt(2f * _climbHeight / -gravity); 

        //launch a bit higher than the ledge
        float _launchSpeed = Mathf.Sqrt(2f * -gravity * (_climbHeight + vaultExtraHeight));
        float _currentUpSpeed = Vector3.Dot(_currentVelocity, _up);
        _currentVelocity += _up * (_launchSpeed - _currentUpSpeed);
    }


    public void PostGroundingUpdate(float _deltaTime)
    {
        if (!Motor.GroundingStatus.IsStableOnGround && State.Stance is Stance.Slide) State.Stance = Stance.Crouch;
    }

    public bool IsColliderValidForCollisions(Collider _coll)
    {
        return true;
    }

    public void OnGroundHit(Collider _hitCollider, Vector3 _hitNormal, Vector3 _hitPoint, ref HitStabilityReport _hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider _hitCollider, Vector3 _hitNormal, Vector3 _hitPoint, ref HitStabilityReport _hitStabilityReport)
    {
    }

    //idfk, also vibecoded for the soccerball but works
    public void ProcessHitStabilityReport(Collider _hitCollider, Vector3 _hitNormal, Vector3 _hitPoint, Vector3 _atCharacterPosition, Quaternion _atCharacterRotation, ref HitStabilityReport _hitStabilityReport)
    {
        if (player == null || !player.IsOwner || player.IsServer) return;
        if (_hitCollider.attachedRigidbody == null) return;
        if (!_hitCollider.attachedRigidbody.TryGetComponent(out PredictedProp _prop)) return;

        float _approachSpeed = Vector3.Dot(State.Velocity, -_hitNormal);
        if (_approachSpeed < minKickSpeed) return;

        if (_prop == lastKickedProp && (tick - lastKickTick) * tickDeltaTime < kickInterval) return;
        lastKickedProp = _prop;
        lastKickTick = tick;

        pendingKickProp = _prop;
        pendingKickForce = -_hitNormal * _approachSpeed * kickForce;
        pendingKickPoint = _hitPoint;
    }

    public void OnDiscreteCollisionDetected(Collider _hitCollider)
    {
    }

    public Vector3 GetValidPosition(Vector3 _position, int _attempts = 0)
    {
        if (_attempts > 16) return _position;

        int _hits = Motor.CharacterOverlap(_position, transform.rotation, uncrouchColliders, LayerMask.GetMask("Player", "Ghost"), QueryTriggerInteraction.Ignore);
        if (_hits <= 0) return _position;

        Vector3 _dir = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
        float _dist = Motor.Capsule.radius * 2f + 0.25f;

        return GetValidPosition(_position + _dir * _dist, _attempts + 1);
    }

    public void SetPosition(Vector3 _position, bool _killVelocity = true)
    {
        pendingTeleport = true;
        pendingTeleportPosition = _position;
        pendingKillVelocity = _killVelocity;
        if (_killVelocity) pendingForce = Vector3.zero;
    }

    public void SetSpectator(bool _spectator)
    {
        spectator = _spectator;
        Motor.CollidableLayers = _spectator ? spectatorLayerMask : playerLayerMask;
    }

    public void AddForce(Vector3 _force)
    {
        if (float.IsNaN(_force.x) || float.IsNaN(_force.y) || float.IsNaN(_force.z)) return;
        pendingForce += _force;
    }
}
