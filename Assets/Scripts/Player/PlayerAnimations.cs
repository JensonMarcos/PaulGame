using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerAnimations : NetworkBehaviour
{
    [SerializeField] BodyAnimation body;
    [SerializeField] HandsAnimation hands;
    [SerializeField] ItemAnimation item;
    [SerializeField] FingerAnimation fingers;
    [SerializeField] PlayerCamera cam;

    [SerializeField] PlayerCharacter character;

    public void Initialize()
    {
        body.Initialize();
        fingers.Initialize();
        //hands.Initialize();
        body.BodyAnimator.enabled = false;
        
    }

    //control animator
    public void UpdateAnimatorValues(PlayerState _state)
    {
        var _stance = _state.Stance is Stance.Stand or Stance.Sprint ? 1f : 0f;
        var _sprint = _state.Stance is Stance.Sprint? 1f : 0f;
        var _slide = _state.Stance is Stance.Slide? 1f : 0f;
        var _idleState = _state.Melee ? 0f : Mathf.Lerp(0.5f, 1f, _state.Aiming);

        //Logic for the walking animation, bacically normalizing but kinda weird
        var _vel = character.transform.InverseTransformDirection(_state.Velocity);

        var _horizontal = 0f;
        var _vertical = 0f;

        var _targetSpeed = _state.Stance switch
        {
            Stance.Stand => character.walkSpeed,
            Stance.Sprint => character.sprintSpeed,
            Stance.Crouch => character.crouchSpeed,
            Stance.Slide => 0f,
            _ => 0f,
        };

        if(!_state.Grounded) _targetSpeed = _state.Velocity.magnitude * 2f;

        if(_targetSpeed != 0f)
        {
            _horizontal = Mathf.Clamp(_vel.x/_targetSpeed, -1, 1);
            _vertical = Mathf.Clamp(_vel.z/_targetSpeed, -1, 1);
        }

        body.SetAnimator(_stance, _sprint, _slide, _horizontal, _vertical, _idleState);
    }

    public void UpdateAnimator(float deltaTime)
    {
        body.BodyAnimator.Update(deltaTime);
    }  

    //adjustments after everything else (animator + cam)
    public void UpdateRigs(PlayerState _state, ItemClient _item, Transform camTarget)
    {
        body.UpdateRigs(_state.Aiming);

        hands.UpdateTransform(camTarget, _state.Reloading);
        bool _ikSprint = _state.Stance == Stance.Sprint && _state.Melee;
        hands.UpdateRigs(_item.RHand, _item.LHand, _item.data.RightHandIK || !_ikSprint, _item.data.LeftHandIK || !_ikSprint);

        Vector3 aimPos = _item.data.position; //if no sight, just keep same position
        if(_item.sight != null)
        {
            aimPos = camTarget.transform.position - _item.sight.position; ;

            aimPos = item.transform.parent.InverseTransformVector(aimPos);
            aimPos = item.transform.localPosition + aimPos;
        }

        item.UpdatePosition(Vector3.Lerp(_item.data.position, aimPos, _state.Aiming));
        item.UpdateRotation(_state.Stance is not Stance.Sprint && !_state.Melee, _state.Aiming, Mathf.Pow(Mathf.Cos(_state.Reloading * Mathf.PI), 10));

        //fingers.UpdateFingers();
    }

    // public void UpdateItem(PlayerState _state, ItemClient _item, Transform camTarget)
    // {
    //     Vector3 aimPos = _item.data.position; //if no sight, just keep same position
    //     if(_item.sight != null)
    //     {
    //         aimPos = camTarget.transform.position - _item.sight.position; ;

    //         aimPos = item.transform.parent.InverseTransformVector(aimPos);
    //         aimPos = item.transform.localPosition + aimPos;
    //     }

    //     item.UpdatePosition(Vector3.Lerp(_item.data.position, aimPos, _state.Aiming));
    //     item.UpdateRotation(_state.Stance is not Stance.Sprint && !_state.Melee, _state.Aiming, Mathf.Pow(Mathf.Cos(_state.Reloading * Mathf.PI), 10));
    // }

    #region Shoot
    [Rpc(SendTo.Server)]
    public void ShootServerRpc(Vector3 _recoil, float _backKick, float _rotKick)
    {
        ShootClientRpc(_recoil, _backKick, _rotKick);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ShootClientRpc(Vector3 _recoil, float _backKick, float _rotKick)
    {
        if(IsOwner) return;
        Shoot(_recoil, _backKick, _rotKick);
    }

    public void Shoot(Vector3 _recoil, float _backKick, float _rotKick)
    {
        cam.AddRotation(_recoil.x, _recoil.y, _recoil.z, 1);
        item.AddTransform(new Vector3(0, 0, _backKick), Quaternion.Euler(_rotKick, 0, 0));
    }
    #endregion

    public void SwitchItemAnimation(float _pullOutTime)
    {
        //hands.ResetHands();
        hands.Pullout(_pullOutTime);
        fingers.GripGun();
    }

    public void SetAnimationActive(bool _active)
    {
        body.gameObject.SetActive(_active);
    }
}