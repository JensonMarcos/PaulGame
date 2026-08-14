using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerAnimations : NetworkBehaviour
{
    [SerializeField] BodyAnimation body;
    [SerializeField] HandsAnimation hands;
    [SerializeField] ItemAnimation item;
    [SerializeField] FingerAnimation fingers;
    public PlayerCamera cam;

    [SerializeField] PlayerCharacter character;
    float reloading;

    public void Initialize()
    {
        body.Initialize();
        fingers.Initialize();
        //hands.Initialize();
        body.BodyAnimator.enabled = false;
        
    }

    //control animator
    public void UpdateAnimatorValues(PlayerState _state, ItemData _item)
    {
        var _stance = _state.Stance is Stance.Stand or Stance.Sprint ? 1f : 0f;
        var _sprint = _state.Stance is Stance.Sprint? 1f : 0f;
        var _slide = _state.Stance is Stance.Slide? 1f : 0f;
        var _idleState = _item.type == ItemType.Melee ? 0f : Mathf.Lerp(0.5f, 1f, _state.Aiming);

        var _vel = character.transform.InverseTransformDirection(_state.Velocity);

        var _horizontal = 0f;
        var _vertical = 0f;

        var _targetSpeed = _state.Stance switch
        {
            Stance.Stand => character.walkSpeed,
            Stance.Sprint => character.sprintSpeed,
            Stance.Crouch => character.crouchSpeed,
            Stance.Slide => 0f,
            Stance.Vault => 0f,
            _ => 0f,
        };

        if(!_state.Grounded) _targetSpeed = _state.Velocity.magnitude * 2f;

        _targetSpeed *= 0.8f; //dont ask why

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
        body.UpdateRigs(_state.Aiming, 1f); //make this lerp

        reloading = _state.Reloading > 0f ? Mathf.Lerp(reloading, _state.Reloading, 30f * Time.deltaTime) : 0f; //MY CODE IS SHIT
        hands.UpdateTransform(camTarget, reloading);

        bool _doIKRight = _item.data.RightHandIK || !(_state.Stance == Stance.Sprint || _state.Stance == Stance.Vault);
        bool _doIKLeft = _item.data.LeftHandIK || !(_state.Stance == Stance.Sprint || _state.Stance == Stance.Vault);

        body.SetLayerWeight(2, _doIKRight ? 1f : 0f);
        //body.SetLayerWeight(3, _doIKLeft ? 1f : 0f);
        
        hands.UpdateRigs(_item.RHand, _item.LHand, _doIKRight, _doIKLeft);

        Vector3 aimPos = _item.data.position; //if no sight, just keep same position
        if(_item.sight != null)
        {
            aimPos = camTarget.transform.position - _item.sight.position; ;

            aimPos = item.transform.parent.InverseTransformVector(aimPos);
            aimPos = item.transform.localPosition + aimPos;
        }

        item.UpdatePosition(Vector3.Lerp(_item.data.position, aimPos, _state.Aiming), IsOwner);
        item.UpdateRotation(_state.Stance is not Stance.Sprint and not Stance.Vault && _item.data.type != ItemType.Melee, _state.Aiming, Mathf.Pow(Mathf.Cos(reloading * Mathf.PI), 10), IsOwner);

        //fingers.UpdateFingers();
    }

    public void SwitchItemAnimation(float _pullOutTime)
    {
        //hands.ResetHands();
        hands.Pullout(_pullOutTime);
        //fingers.GripGun();
    }

    public void SetAnimationActive(bool _active)
    {
        body.gameObject.SetActive(_active);
    }

    public void SetUpperBodyTilt(float _tilt)
    {
        body.UpperBodyTilt = _tilt;
    }

    #region TriggerAnimation
    [Rpc(SendTo.Server)]
    public void TriggerAnimationServerRpc(string name, RpcParams rpcParams = default)
    {
        TriggerAnimationClientRpc(name, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void TriggerAnimationClientRpc(string name, RpcParams rpcParams = default)
    {
        TriggerAnimation(name);
    }

    public void TriggerAnimation(string name)
    {

        body.TriggerAnimator(name);
    }
    #endregion

    public void Shoot(Vector3 _recoil, float _backKick, float _rotKick)
    {
        cam.AddRotation(_recoil.x, _recoil.y, _recoil.z, 1);
        item.AddTransform(new Vector3(0, 0, _backKick), Quaternion.Euler(_rotKick, 0, 0));
    }

    #region HandPush
    [Rpc(SendTo.Server)]
    public void HandPushServerRpc(Vector3 force, RpcParams rpcParams = default)
    {
        HandPushClientRpc(force, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    public void HandPushClientRpc(Vector3 force, RpcParams rpcParams = default)
    {
        HandPush(force);
    }

    public void HandPush(Vector3 force)
    {
        item.AddTransform(force, Quaternion.Euler(0, 0, 0));
    }
    #endregion


}