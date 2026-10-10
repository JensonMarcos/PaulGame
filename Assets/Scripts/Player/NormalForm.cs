using UnityEngine;

public class NormalForm : MonoBehaviour, IPlayerForm
{
    [SerializeField] Player player;
    [SerializeField] PlayerCharacter character;
    [SerializeField] PlayerAnimations animations;
    [SerializeField] PlayerCombat combat;
    [SerializeField] PlayerInventory inventory;

    public Transform CamTarget => character.camTarget;

    ItemClient Equipped(PlayerState _state) => inventory.ClientInventory[_state.InventoryIndex];

    public void Enter()
    {
        character.root.gameObject.SetActive(true);
    }

    public void Exit()
    {
        character.root.gameObject.SetActive(false);
        if(!player.IsOwner) return;

        inventory.DropAll();
        inventory.Deselect();
    }

    public void SetDead(bool _dead)
    {
        if(player.IsOwner) animations.SetAnimationActive(!_dead);
        else character.root.gameObject.SetActive(!_dead);
    }

    public void Tick(CharacterInputs _inputs, int _tick, float _deltaTime)
    {
        character.Tick(_inputs, _tick, _deltaTime);
    }

    public void WriteState(ref PlayerState _state)
    {
        character.WriteState(ref _state);

        ItemClient _equipped = Equipped(_state);
        _state.Aiming = _equipped.Aiming;
        _state.ReadyPull = inventory.ReadyPull;
        _state.Reloading = _equipped.Reloading;
    }

    public void PlayStateEvents(PlayerState _previous, PlayerState _current)
    {
        if(_current.Stance == Stance.Vault && _previous.Stance != Stance.Vault) animations.TriggerAnimation("Vault");
        if(_current.WallJumps != _previous.WallJumps) animations.HandPush(_current.HandPush);
    }

    public void UpdateAnimations(PlayerState _state, float _deltaTime)
    {
        animations.UpdateAnimatorValues(_state, Equipped(_state));
        animations.UpdateAnimator(_deltaTime);
    }

    public void UpdateRigs(PlayerState _state)
    {
        animations.UpdateRigs(_state, Equipped(_state), character.camTarget);
    }

    public void SetCombatInputs(CombatInputs _inputs)
    {
        combat.SetInputs(_inputs, inventory.ReadyPull);
    }

    public void UpdateCombat(PlayerState _state)
    {
        inventory.TryPickUp();
        combat.UpdateCombat(_state, Equipped(_state));
    }

    public void AddForce(Vector3 _force) => character.AddForce(_force);

    public void SetPosition(Vector3 _position) => character.SetPosition(_position);
}
