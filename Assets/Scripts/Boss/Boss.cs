using Unity.Netcode;
using UnityEngine;

public abstract class Boss : NetworkBehaviour, IPlayerForm
{
    [SerializeField] GameObject body;
    [SerializeField] Transform camTarget;
    [SerializeField] float capsuleRadius = 1f;
    [SerializeField] float capsuleHeight = 4f;
    [SerializeField] float mass = 5f; //scales down incoming forces (knockback, recoil); 1 = normal player
    [SerializeField] BossMovement movement;
    [SerializeField] PlayerAnimations animations;
    [SerializeField] ItemClient hands; //hand pose when holding no items, not ticked as an item
    [SerializeField] ItemClient[] items;

    protected Player player;
    protected PlayerCharacter character;
    protected PlayerCombat combat;
    protected CombatInputs inputs;

    float normalRadius;

    public Transform CamTarget => camTarget;
    public ItemClient Equipped => items.Length > 0 ? items[0] : hands;
    public virtual ItemClient Melee => Equipped;
    public PlayerAnimations Animations => animations;

    protected virtual ItemClient RightItem => Equipped;
    protected virtual ItemClient LeftItem => Equipped;

    public override void OnNetworkSpawn() => Attach();

    public override void OnNetworkObjectParentChanged(NetworkObject _parentNetworkObject) => Attach();

    public override void OnNetworkDespawn()
    {
        if(player != null) player.ClearForm(this);
    }

    //spawned as a child of the player it belongs to
    void Attach()
    {
        if(player != null) return;

        player = GetComponentInParent<Player>();
        if(player == null) return;

        character = player.playerCharacter;
        combat = player.GetComponent<PlayerCombat>();
        animations.Initialize(player.playerCamera);
        player.SetForm(this);
    }

    public virtual void Enter()
    {
        body.SetActive(true);

        normalRadius = character.Motor.Capsule.radius;
        character.Motor.SetCapsuleDimensions(capsuleRadius, capsuleHeight, capsuleHeight * 0.5f);

        movement.Initialize(character.Motor);
        character.Motor.CharacterController = movement;
    }

    public virtual void Exit()
    {
        body.SetActive(false);

        character.Motor.CharacterController = character;
        character.Motor.SetCapsuleDimensions(normalRadius, character.standHeight, character.standHeight * 0.5f);
    }

    public virtual void SetDead(bool _dead) => body.SetActive(!_dead);

    public void Tick(CharacterInputs _inputs, int _tick, float _deltaTime) => movement.Tick(_inputs, _deltaTime);

    public void WriteState(ref PlayerState _state)
    {
        movement.WriteState(ref _state);

        _state.Aiming = Equipped.Aiming;
        _state.ReadyPull = true;
        _state.Reloading = Equipped.Reloading;
    }

    public virtual void PlayStateEvents(PlayerState _previous, PlayerState _current) { }

    public virtual void UpdateAnimations(PlayerState _state, float _deltaTime)
    {
        animations.UpdateAnimatorValues(_state, Equipped, movement.StanceSpeed(_state.Stance), RightItem, LeftItem);
        animations.UpdateAnimator(_deltaTime);
    }

    public virtual void UpdateRigs(PlayerState _state)
    {
        transform.SetPositionAndRotation(character.transform.position, character.transform.rotation);
        animations.UpdateRigs(_state, Equipped, camTarget, RightItem, LeftItem);
    }

    public void SetCombatInputs(CombatInputs _inputs)
    {
        inputs = _inputs;
        combat.SetInputs(ItemInputs(_inputs), true);
    }

    protected virtual CombatInputs ItemInputs(CombatInputs _inputs) => _inputs;

    public virtual void UpdateCombat(PlayerState _state)
    {
        if(items.Length > 0) combat.UpdateCombat(_state, Equipped);
    }

    public void AddForce(Vector3 _force) => movement.AddForce(_force / Mathf.Max(mass, 0.01f));

    public void SetPosition(Vector3 _position) => movement.SetPosition(_position);
}
