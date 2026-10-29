using UnityEngine;

public class PaulBossMovement : BossMovement
{
    [SerializeField] float walkSpeed = 6.25f;
    [SerializeField] float sprintSpeed = 10f;
    [SerializeField] float crouchSpeed = 3f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float airAcceleration = 0.5f;
    [SerializeField] float jumpSpeed = 8f;
    [SerializeField] float gravity = -30f;

    [Header("Super Jump")]
    [SerializeField] float jumpChargeTime = 1.5f; //seconds of crouching to fully charge
    [SerializeField] float jumpUnchargeTime = 0.4f; //seconds to fully drain when not crouching
    [SerializeField] float superJumpSpeed = 14f; //jump speed at full charge (normal jumpSpeed at 0)
    [SerializeField] float superJumpLeapSpeed = 16f; //speed added in the direction the camera faces at full charge (none at 0), on top of superJumpSpeed
    [SerializeField] float rechargeCooldown = 1f; //seconds after a jump before charging can start again (cleared by landing a full super jump)
    [SerializeField, Range(0f, 1f)] float superJumpMinCharge = 0.98f; //charge needed at takeoff to count as a full super jump (landing shockwave)

    float cooldownTimer;
    bool superJumpAirborne; //took off with a full charge and hasnt landed yet
    bool leftGround; //has actually left the ground since that jump

    public float JumpCharge { get; private set; } //0 to 1
    public bool Charging { get; private set; } //crouched on the ground and able to charge
    public byte SuperLandings { get; private set; } //goes up every time a full super jump lands

    public override float StanceSpeed(Stance _stance) => _stance switch
    {
        Stance.Sprint => sprintSpeed,
        Stance.Crouch => crouchSpeed,
        _ => walkSpeed,
    };

    public override void UpdateVelocity(ref Vector3 _currentVelocity, float _deltaTime)
    {
        cooldownTimer = Mathf.Max(0f, cooldownTimer - _deltaTime);

        bool _grounded = Motor.GroundingStatus.IsStableOnGround;

        //charging only happens on the ground
        Charging = crouching && _grounded && cooldownTimer <= 0f;
        if (Charging) JumpCharge += _deltaTime / Mathf.Max(jumpChargeTime, 0.01f);
        else if (!crouching) JumpCharge -= _deltaTime / Mathf.Max(jumpUnchargeTime, 0.01f);
        JumpCharge = Mathf.Clamp01(JumpCharge);

        if (_grounded)
        {
            //landed from a full super jump
            if (superJumpAirborne && leftGround)
            {
                superJumpAirborne = false;
                cooldownTimer = 0f;
                SuperLandings++;
            }

            Vector3 _groundedMovement = Motor.GetDirectionTangentToSurface(wishMovement, Motor.GroundingStatus.GroundNormal) * wishMovement.magnitude;
            _currentVelocity = Vector3.Lerp(_currentVelocity, _groundedMovement * StanceSpeed(CurrentStance), 1f - Mathf.Exp(-acceleration * _deltaTime));

            if (inputs.Jump)
            {
                Motor.ForceUnground(0.1f);
                float _verticalSpeed = Vector3.Dot(_currentVelocity, Motor.CharacterUp);
                float _launchSpeed = Mathf.Lerp(jumpSpeed, superJumpSpeed, JumpCharge);
                _currentVelocity += Motor.CharacterUp * (Mathf.Max(_verticalSpeed, _launchSpeed) - _verticalSpeed);

                //looking down only pushes horizontally so it cant cancel out the jump
                Vector3 _leapDir = inputs.CameraRotation * Vector3.forward;
                float _leapUp = Vector3.Dot(_leapDir, Motor.CharacterUp);
                if (_leapUp < 0f) _leapDir -= Motor.CharacterUp * _leapUp;
                _currentVelocity += _leapDir * superJumpLeapSpeed * JumpCharge;

                superJumpAirborne = JumpCharge >= superJumpMinCharge;
                leftGround = false;
                JumpCharge = 0f;
                cooldownTimer = rechargeCooldown;
            }
        }
        else
        {
            leftGround = true;

            //air strafing, same as the normal character without the deceleration
            Vector3 _planarVel = Vector3.ProjectOnPlane(_currentVelocity, Motor.CharacterUp);
            float _addSpeed = airAcceleration - Vector3.Dot(_planarVel, wishMovement);
            if (_addSpeed > 0f) _currentVelocity += wishMovement * _addSpeed;

            _currentVelocity += Motor.CharacterUp * gravity * _deltaTime;
        }
    }
}
