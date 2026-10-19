using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerAnimations : NetworkBehaviour
{
    [SerializeField] BodyAnimation body;
    [SerializeField] HandsAnimation hands;
    [SerializeField] ItemAnimation item;
    [SerializeField] FingerAnimation fingers;
    public PlayerCamera Cam { get; private set; }
    public Transform ItemRoot => item.transform;

    float reloading;

    public void Initialize(PlayerCamera _cam)
    {
        Cam = _cam;
        body.Initialize(_cam.transform);
        item.Initialize(_cam.transform);
        fingers.Initialize();
        //hands.Initialize();
        body.BodyAnimator.enabled = false;
        
    }

    //control animator
    //_rightItem/_leftItem drive a hand that isn't holding _item
    public void UpdateAnimatorValues(PlayerState _state, ItemClient _item, float _stanceSpeed, ItemClient _rightItem = null, ItemClient _leftItem = null)
    {
        if(_rightItem == null) _rightItem = _item;
        if(_leftItem == null) _leftItem = _item;

        var _stance = _state.Stance is Stance.Stand or Stance.Sprint ? 1f : 0f;
        var _sprint = _state.Stance is Stance.Sprint? 1f : 0f;
        var _slide = _state.Stance is Stance.Slide? 1f : 0f;
        var _idleState = IdleState(_rightItem, _state.Aiming); //upper body follows the right hand
        var _leftIdleState = IdleState(_leftItem, _state.Aiming);

        var _vel = body.transform.InverseTransformDirection(_state.Velocity);

        var _horizontal = 0f;
        var _vertical = 0f;

        var _targetSpeed = _stanceSpeed;

        if(!_state.Grounded) _targetSpeed = _state.Velocity.magnitude * 2f;

        _targetSpeed *= 0.8f; //dont ask why

        if(_targetSpeed != 0f)
        {
            _horizontal = Mathf.Clamp(_vel.x/_targetSpeed, -1, 1);
            _vertical = Mathf.Clamp(_vel.z/_targetSpeed, -1, 1);
        }

        body.SetAnimator(_stance, _sprint, _slide, _horizontal, _vertical, _idleState, _leftIdleState);
    }

    static float IdleState(ItemClient _item, float _aiming) => _item.IdleIsMelee ? 0f : Mathf.Lerp(0.5f, 1f, _aiming);

    public void UpdateAnimator(float deltaTime)
    {
        body.BodyAnimator.Update(deltaTime);
    }  

    //adjustments after everything else (animator + cam)
    public void UpdateRigs(PlayerState _state, ItemClient _item, Transform camTarget, ItemClient _rightItem = null, ItemClient _leftItem = null)
    {
        if(_rightItem == null) _rightItem = _item;
        if(_leftItem == null) _leftItem = _item;

        body.UpdateRigs(_state.Aiming, 1f); //make this lerp

        reloading = _state.Reloading > 0f ? Mathf.Lerp(reloading, _state.Reloading, 30f * Time.deltaTime) : 0f; //MY CODE IS SHIT
        hands.UpdateTransform(camTarget);

        bool _doIKRight = _rightItem.rightHandIK || !(_state.Stance == Stance.Sprint || _state.Stance == Stance.Vault);
        bool _doIKLeft = _leftItem.leftHandIK || !(_state.Stance == Stance.Sprint || _state.Stance == Stance.Vault);

        body.SetLayerWeight(2, _doIKRight ? 1f : 0f);
        body.SetLayerWeight(3, _doIKLeft ? 1f : 0f);
        
        hands.UpdateRigs(_rightItem.RHand, _leftItem.LHand, _doIKRight, _doIKLeft);

        Vector3 aimPos = _item.holdPosition;
        if(_item.Sight != null)
        {
            aimPos = camTarget.transform.position - _item.Sight.position;

            aimPos = item.transform.parent.InverseTransformVector(aimPos);
            aimPos = item.transform.localPosition + aimPos;
        }

        item.UpdatePosition(Vector3.Lerp(_item.holdPosition, aimPos, _state.Aiming), IsOwner);
        item.UpdateRotation(_state.Stance is not Stance.Sprint and not Stance.Vault && !_item.IdleIsMelee, _state.Aiming, Mathf.Pow(Mathf.Cos(reloading * Mathf.PI), 10), IsOwner);
        item.UpdateReload(reloading);

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

    public void SetFirstBoneWeight(bool right, float weight)
    {
        hands.SetFirstBoneWeight(right, weight);
    }

    public void ResetFirstBoneWeights()
    {
        hands.ResetFirstBoneWeights();
    }

    public void TriggerAnimation(string name)
    {

        body.TriggerAnimator(name);
    }

    public void Shoot(Vector3 _recoil, float _backKick, float _rotKick)
    {
        Cam.AddRotation(_recoil.x, _recoil.y, _recoil.z, 1);
        item.AddTransform(new Vector3(0, 0, _backKick), Quaternion.Euler(_rotKick, 0, 0));
    }

    public void HandPush(Vector3 force)
    {
        item.AddTransform(force, Quaternion.Euler(0, 0, 0));
    }


}