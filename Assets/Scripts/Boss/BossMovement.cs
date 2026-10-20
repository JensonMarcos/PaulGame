using KinematicCharacterController;
using UnityEngine;

public abstract class BossMovement : MonoBehaviour, ICharacterController
{
    protected KinematicCharacterMotor Motor;
    protected CharacterInputs inputs;
    protected Vector3 wishMovement;

    [SerializeField] float crouchHeight = 2f;
    protected bool crouching;
    float standHeight;
    Collider[] uncrouchColliders = new Collider[8];

    Vector3 pendingForce;
    Vector3? pendingTeleport;
    byte teleports;

    public void Initialize(KinematicCharacterMotor _motor)
    {
        Motor = _motor;
        standHeight = Motor.Capsule.height; //the boss has already sized the capsule by now
        crouching = false;
    }

    public void Tick(CharacterInputs _inputs, float _deltaTime)
    {
        if (pendingTeleport.HasValue)
        {
            Motor.SetPositionAndRotation(pendingTeleport.Value, Motor.TransientRotation);
            pendingTeleport = null;
            Motor.BaseVelocity = Vector3.zero;
            teleports++;
        }
        if (pendingForce != Vector3.zero)
        {
            Motor.ForceUnground();
            Motor.BaseVelocity += pendingForce;
            pendingForce = Vector3.zero;
        }

        inputs = _inputs;
        wishMovement = Motor.TransientRotation * Vector3.ClampMagnitude(new Vector3(_inputs.RightAxis, 0f, _inputs.ForwardAxis), 1f);

        KinematicCharacterSystem.PreSimulationInterpolationUpdate(_deltaTime);
        KinematicCharacterSystem.Simulate(_deltaTime, KinematicCharacterSystem.CharacterMotors, KinematicCharacterSystem.PhysicsMovers);
        KinematicCharacterSystem.PostSimulationInterpolationUpdate(_deltaTime);
    }

    public void WriteState(ref PlayerState _state)
    {
        _state.Position = Motor.TransientPosition;
        _state.Grounded = Motor.GroundingStatus.IsStableOnGround;
        if (crouching) _state.Stance = Stance.Crouch;
        else _state.Stance = inputs.Sprint && wishMovement != Vector3.zero ? Stance.Sprint : Stance.Stand;
        _state.Velocity = Motor.Velocity;
        _state.Teleports = teleports;
    }

    public void AddForce(Vector3 _force)
    {
        if (float.IsNaN(_force.x) || float.IsNaN(_force.y) || float.IsNaN(_force.z)) return;
        pendingForce += _force;
    }

    public void SetPosition(Vector3 _position)
    {
        pendingTeleport = _position;
        pendingForce = Vector3.zero;
    }

    public abstract float StanceSpeed(Stance _stance);

    public abstract void UpdateVelocity(ref Vector3 _currentVelocity, float _deltaTime);

    public virtual void UpdateRotation(ref Quaternion _currentRotation, float _deltaTime)
    {
        Vector3 _forward = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp);
        if (_forward != Vector3.zero) _currentRotation = Quaternion.LookRotation(_forward, Motor.CharacterUp);
    }

    public virtual void BeforeCharacterUpdate(float _deltaTime)
    {
        if (inputs.Crouch && !crouching)
        {
            crouching = true;
            Motor.SetCapsuleDimensions(Motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
        }
    }

    public virtual void PostGroundingUpdate(float _deltaTime) { }

    public virtual void AfterCharacterUpdate(float _deltaTime)
    {
        if (inputs.Crouch || !crouching) return;

        //only stand back up if theres room
        Motor.SetCapsuleDimensions(Motor.Capsule.radius, standHeight, standHeight * 0.5f);
        if (Motor.CharacterOverlap(Motor.TransientPosition, Motor.TransientRotation, uncrouchColliders, Motor.CollidableLayers, QueryTriggerInteraction.Ignore) > 0)
            Motor.SetCapsuleDimensions(Motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
        else
            crouching = false;
    }

    public virtual bool IsColliderValidForCollisions(Collider _coll) => true;
    public virtual void OnGroundHit(Collider _hitCollider, Vector3 _hitNormal, Vector3 _hitPoint, ref HitStabilityReport _hitStabilityReport) { }
    public virtual void OnMovementHit(Collider _hitCollider, Vector3 _hitNormal, Vector3 _hitPoint, ref HitStabilityReport _hitStabilityReport) { }
    public virtual void ProcessHitStabilityReport(Collider _hitCollider, Vector3 _hitNormal, Vector3 _hitPoint, Vector3 _atCharacterPosition, Quaternion _atCharacterRotation, ref HitStabilityReport _hitStabilityReport) { }
    public virtual void OnDiscreteCollisionDetected(Collider _hitCollider) { }
}
