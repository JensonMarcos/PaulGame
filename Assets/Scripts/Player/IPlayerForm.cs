using UnityEngine;

public interface IPlayerForm
{
    Transform CamTarget { get; }

    void Enter();
    void Exit();
    void SetDead(bool _dead);

    void Tick(CharacterInputs _inputs, int _tick, float _deltaTime);
    void WriteState(ref PlayerState _state);
    void PlayStateEvents(PlayerState _previous, PlayerState _current);

    void UpdateAnimations(PlayerState _state, float _deltaTime);
    void UpdateRigs(PlayerState _state);

    void SetCombatInputs(CombatInputs _inputs);
    void UpdateCombat(PlayerState _state);

    void AddForce(Vector3 _force);
    void SetPosition(Vector3 _position);
}
