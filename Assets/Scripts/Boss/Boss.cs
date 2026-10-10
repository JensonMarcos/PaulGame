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

    protected Player player;
    protected PlayerCharacter character;
    protected PlayerCombat combat;
    protected CombatInputs inputs;

    float normalRadius;

    public Transform CamTarget => camTarget;

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

        _state.Aiming = 0f;
        _state.ReadyPull = false;
        _state.Reloading = 0f;
    }

    public virtual void PlayStateEvents(PlayerState _previous, PlayerState _current) { }

    public virtual void UpdateAnimations(PlayerState _state, float _deltaTime) { }

    public virtual void UpdateRigs(PlayerState _state)
    {
        transform.SetPositionAndRotation(character.transform.position, character.transform.rotation);
    }

    public void SetCombatInputs(CombatInputs _inputs) => inputs = _inputs;

    public abstract void UpdateCombat(PlayerState _state);

    public void AddForce(Vector3 _force) => movement.AddForce(_force / Mathf.Max(mass, 0.01f));

    public void SetPosition(Vector3 _position) => movement.SetPosition(_position);
}
