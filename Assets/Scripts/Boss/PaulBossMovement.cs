using UnityEngine;

public class PaulBossMovement : BossMovement
{
    [SerializeField] float walkSpeed = 6.25f;
    [SerializeField] float sprintSpeed = 10f;
    [SerializeField] float acceleration = 12f;
    [SerializeField] float airAcceleration = 0.5f;
    [SerializeField] float jumpSpeed = 8f;
    [SerializeField] float gravity = -30f;

    public override void UpdateVelocity(ref Vector3 _currentVelocity, float _deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            Vector3 _groundedMovement = Motor.GetDirectionTangentToSurface(wishMovement, Motor.GroundingStatus.GroundNormal) * wishMovement.magnitude;
            float _speed = inputs.Sprint ? sprintSpeed : walkSpeed;
            _currentVelocity = Vector3.Lerp(_currentVelocity, _groundedMovement * _speed, 1f - Mathf.Exp(-acceleration * _deltaTime));

            if (inputs.Jump)
            {
                Motor.ForceUnground(0.1f);
                float _verticalSpeed = Vector3.Dot(_currentVelocity, Motor.CharacterUp);
                _currentVelocity += Motor.CharacterUp * (Mathf.Max(_verticalSpeed, jumpSpeed) - _verticalSpeed);
            }
        }
        else
        {
            //air strafing, same as the normal character without the deceleration
            Vector3 _planarVel = Vector3.ProjectOnPlane(_currentVelocity, Motor.CharacterUp);
            float _addSpeed = airAcceleration - Vector3.Dot(_planarVel, wishMovement);
            if (_addSpeed > 0f) _currentVelocity += wishMovement * _addSpeed;

            _currentVelocity += Motor.CharacterUp * gravity * _deltaTime;
        }
    }
}
