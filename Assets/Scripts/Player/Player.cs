using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct PlayerState : INetworkSerializable
{
    public int Tick;

    [Header("Character")]
    public Vector3 Position;
    public float CameraPitch;
    public float CameraYaw;
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;
    public byte WallJumps;
    public Vector3 HandPush;
    public byte Teleports;

    [Header("Combat")]
    public int InventoryIndex;
    public float Aiming;
    public bool ReadyPull;
    public float Reloading;

    public void NetworkSerialize<T>(BufferSerializer<T> _serializer) where T : IReaderWriter
    {
        _serializer.SerializeValue(ref Tick);

        ushort _cameraPitch = CompressAngle(CameraPitch);
        ushort _cameraYaw = CompressAngle(CameraYaw);
        _serializer.SerializeValue(ref Position);
        _serializer.SerializeValue(ref _cameraPitch);
        _serializer.SerializeValue(ref _cameraYaw);
        _serializer.SerializeValue(ref Grounded);
        _serializer.SerializeValue(ref Stance);
        SerializeHalf(_serializer, ref Velocity);
        _serializer.SerializeValue(ref WallJumps);
        SerializeHalf(_serializer, ref HandPush);
        _serializer.SerializeValue(ref Teleports);

        byte _inventoryIndex = (byte)InventoryIndex;
        byte _aiming = Compress01(Aiming);
        byte _reloading = Compress01(Reloading);
        _serializer.SerializeValue(ref _inventoryIndex);
        _serializer.SerializeValue(ref _aiming);
        _serializer.SerializeValue(ref ReadyPull);
        _serializer.SerializeValue(ref _reloading);

        if (_serializer.IsReader)
        {
            CameraPitch = DecompressAngle(_cameraPitch);
            CameraYaw = DecompressAngle(_cameraYaw);
            InventoryIndex = _inventoryIndex;
            Aiming = Decompress01(_aiming);
            Reloading = Decompress01(_reloading);
        }
    }

    static byte Compress01(float _value)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(_value) * 255f);
    }

    static float Decompress01(byte _value)
    {
        return _value / 255f;
    }

    static ushort CompressAngle(float _angle)
    {
        return (ushort)Mathf.RoundToInt(Mathf.Repeat(_angle, 360f) / 360f * 65535f);
    }

    static float DecompressAngle(ushort _value)
    {
        return _value / 65535f * 360f;
    }

    static void SerializeHalf<T>(BufferSerializer<T> _serializer, ref Vector3 _value) where T : IReaderWriter
    {
        ushort _x = Mathf.FloatToHalf(_value.x);
        ushort _y = Mathf.FloatToHalf(_value.y);
        ushort _z = Mathf.FloatToHalf(_value.z);
        _serializer.SerializeValue(ref _x);
        _serializer.SerializeValue(ref _y);
        _serializer.SerializeValue(ref _z);
        if (_serializer.IsReader) _value = new Vector3(Mathf.HalfToFloat(_x), Mathf.HalfToFloat(_y), Mathf.HalfToFloat(_z));
    }
}

public class Player : NetworkBehaviour
{
    // Capsules of players this machine doesn't own, for locally simulated props to ignore.
    public static readonly List<Collider> RemoteColliders = new List<Collider>();
    public static event System.Action<Collider> RemoteColliderAdded;
    
    public PlayerState playerState;

    public NetworkVariable<int> Team = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<ulong> SteamId = new NetworkVariable<ulong>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    PlayerInputs playerInputs;
    CharacterInputs characterInputs;

    public PlayerInventory playerInventory;
    public PlayerCharacter playerCharacter;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] PlayerAnimations playerAnimations;
    [SerializeField] NormalForm normalForm;
    [SerializeField] PlayerUI playerUI;
    [SerializeField] PlayerSync playerSync;
    [SerializeField] BodyMaterials bodyMaterials;
    [SerializeField] float deathCamDuration = 3f;

    bool isDead;
    Transform deathCamTarget;
    Coroutine deathCamRoutine;

    public IPlayerForm ActiveForm { get; private set; }

    void Awake()
    {
        ActiveForm = normalForm;
    }
  
    public override void OnNetworkSpawn()
    {
        playerInputs = new PlayerInputs();
        playerInputs.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(ActiveForm.CamTarget, IsOwner);
        playerAnimations.Initialize();
        playerUI.Initialize(IsOwner, OwnerClientId);
        playerInventory.Initialize();

        if(IsOwner && GameManager.instance != null)
        {
            GameManager.instance.GameTitle.OnValueChanged += playerUI.hud.OnTitleChanged;
        }

        if(IsOwner)
        {
            KinematicCharacterSystem.EnsureCreation();
            KinematicCharacterSystem.Settings.AutoSimulation = false;
            NetworkManager.NetworkTickSystem.Tick += OnTick;
        }
        else
        {
            playerCharacter.Motor.enabled = false;
            RegisterRemoteCollider();
        }

        Team.OnValueChanged += OnTeamChanged;
        bodyMaterials.ApplyTeamColor(Team.Value);

        SteamId.OnValueChanged += OnSteamIdChanged;
        bodyMaterials.ApplyFace(SteamId.Value);
        if(IsOwner) SetSteamIdServerRpc(Steamworks.SteamClient.IsValid ? Steamworks.SteamClient.SteamId.Value : 0);
    }

    public override void OnNetworkDespawn()
    {
        if(IsServer && PlayerManager.instance != null && !NetworkManager.ShutdownInProgress)
            PlayerManager.instance.DespawnInventoryItems(this);

        if(IsOwner && GameManager.instance != null)
            GameManager.instance.GameTitle.OnValueChanged -= playerUI.hud.OnTitleChanged;

        if(IsOwner && NetworkManager.NetworkTickSystem != null)
            NetworkManager.NetworkTickSystem.Tick -= OnTick;

        Team.OnValueChanged -= OnTeamChanged;
        SteamId.OnValueChanged -= OnSteamIdChanged;

        if(!IsOwner) RemoteColliders.Remove(playerCharacter.Motor.Capsule);

        playerInputs.Dispose();
    }

    void OnTeamChanged(int _previous, int _current) => bodyMaterials.ApplyTeamColor(_current);

    void OnSteamIdChanged(ulong _previous, ulong _current) => bodyMaterials.ApplyFace(_current);

    [Rpc(SendTo.Server)]
    void SetSteamIdServerRpc(ulong _steamId, RpcParams _rpcParams = default)
    {
        if(_rpcParams.Receive.SenderClientId != OwnerClientId) return;

        SteamId.Value = _steamId;
        if(PlayerManager.instance != null) PlayerManager.instance.SetPlayerSteamId(OwnerClientId, _steamId);
    }

    public void SetForm(IPlayerForm _form)
    {
        if(_form == ActiveForm) return;

        ActiveForm.Exit();
        ActiveForm = _form;
        ActiveForm.Enter();
        ActiveForm.SetDead(isDead);
    }

    public void ClearForm(IPlayerForm _form)
    {
        if(_form == ActiveForm) SetForm(normalForm);
    }

    void RegisterRemoteCollider()
    {
        Collider _capsule = playerCharacter.Motor.Capsule;
        if(_capsule == null || RemoteColliders.Contains(_capsule)) return;

        RemoteColliders.Add(_capsule);
        RemoteColliderAdded?.Invoke(_capsule);
    }

    void OnTick()
    {
        int _tick = NetworkManager.LocalTime.Tick;
        PlayerState _previous = playerState;

        ActiveForm.Tick(characterInputs, _tick, 1f / NetworkManager.NetworkConfig.TickRate);
        characterInputs.Jump = false;

        UpdateState();
        playerState.Tick = _tick;
        if(!isDead) ActiveForm.PlayStateEvents(_previous, playerState);

        playerSync.Send(playerState);
    }

    void Update()
    {
        if(IsOwner)
        {
            HandleInputs();
        }
        else if(playerSync.TryGetState(out PlayerState _state))
        {
            if(!isDead) ActiveForm.PlayStateEvents(playerState, _state);
            playerState = _state;

            playerCharacter.transform.position = playerState.Position;
            playerCamera.transform.rotation = Quaternion.Euler(playerState.CameraPitch, playerState.CameraYaw, 0f);
        }

        if(!isDead) ActiveForm.UpdateAnimations(playerState, Time.deltaTime);

        #if UNITY_EDITOR
        if(IsOwner && isDead && Keyboard.current.pKey.wasPressedThisFrame) {
            PlayerManager.instance.DEBUGRespawnServerRpc(OwnerClientId);      
        }
        if(IsOwner && Keyboard.current.bKey.wasPressedThisFrame) {
            PlayerManager.instance.DEBUGToggleBossServerRpc(OwnerClientId);
        }
        #endif
    }

    void LateUpdate()
    {
        //single state refresh per frame, after the character motor has moved
        if(IsOwner) UpdateState();
        else if(!isDead) playerCharacter.SetYawFromCamera(playerCamera.transform.rotation);

        ItemClient _item = playerInventory.ClientInventory[playerState.InventoryIndex];

        if(!isDead) ActiveForm.UpdateRigs(playerState);

        Transform _camFollow = deathCamTarget != null ? deathCamTarget : ActiveForm.CamTarget;
        playerCamera.UpdatePosition(_camFollow);
        if (deathCamTarget != null)
            playerCamera.UpdateDeathCamRotation(deathCamTarget);

        if(IsOwner) {
            if(!isDead)
            {
                ActiveForm.UpdateCombat(playerState);
                playerCamera.UpdateCam(_item.AdsZoom, playerState.Aiming);
            }

            playerUI.UpdateUI(playerState, _item);
        }
    }


    void HandleInputs()
    {
        PlayerInputs.GameplayActions _inputs = playerInputs.Gameplay;

        if(_inputs.Escape.WasPressedThisFrame() && PauseMenu.instance != null)
            PauseMenu.instance.SetOpen(!PauseMenu.IsOpen);

        if (PauseMenu.IsOpen)
        {
            characterInputs = new CharacterInputs { CameraRotation = playerCamera.transform.rotation };
            playerInventory.SetInputs(new InventoryInputs { Velocity = playerCharacter.State.Velocity });
            ActiveForm.SetCombatInputs(default);
            playerUI.SetInputs(false);
            return;
        }

        if (deathCamTarget == null)
        {
            Vector2 _cameraInputs = _inputs.Look.ReadValue<Vector2>();
            playerCamera.UpdateRotation(_cameraInputs, playerInventory.ClientInventory[playerState.InventoryIndex]);
        }

        //gathered every frame, used by the next tick
        characterInputs = new CharacterInputs {
            ForwardAxis = _inputs.Move.ReadValue<Vector2>().y,
            RightAxis = _inputs.Move.ReadValue<Vector2>().x,
            CameraRotation = playerCamera.transform.rotation,
            Jump = characterInputs.Jump || _inputs.Jump.WasPressedThisFrame(), //held until a tick uses it
            Crouch = _inputs.Crouch.IsPressed(),
            Sprint = _inputs.Sprint.IsPressed()
        };

        InventoryInputs _inventoryInputs = new InventoryInputs {
            Interact = _inputs.Interact.WasPressedThisFrame(),
            Drop = _inputs.Drop.WasPressedThisFrame(),
            Velocity = playerCharacter.State.Velocity,
            //Scroll = inputs.Scroll.ReadValue<float>(),
            //NumKeys = (int)inputs.NumKeys.ReadValue<float>()-1
        };
        playerInventory.SetInputs(_inventoryInputs);


        CombatInputs _combatInputs = new CombatInputs {
            FirePressed = _inputs.Attack.WasPressedThisFrame(),
            FireHeld = _inputs.Attack.IsPressed(),
            FireReleased = _inputs.Attack.WasReleasedThisFrame(),
            AltPressed = _inputs.Aim.WasPressedThisFrame(),
            AltHeld = _inputs.Aim.IsPressed(),
            AltReleased = _inputs.Aim.WasReleasedThisFrame(),
            ReloadPressed = PlayerManager.instance.reloadEnabled.Value && _inputs.Reload.WasPressedThisFrame()
        };
        ActiveForm.SetCombatInputs(_combatInputs);

        playerUI.SetInputs(_inputs.Tab.IsPressed());
    }

    void UpdateState()
    {
        playerState.CameraPitch = playerCamera.transform.eulerAngles.x;
        playerState.CameraYaw = playerCamera.transform.eulerAngles.y;
        playerState.InventoryIndex = playerInventory.InvIndex;

        ActiveForm.WriteState(ref playerState);
    }

    [Rpc(SendTo.Owner)]
    public void UpdateHealthClientRpc(float _health) {
        playerUI.hud.UpdateHealth(_health);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DieClientRpc(ulong _ragdollNetworkId) {
        isDead = true;

        playerCharacter.gameObject.layer = LayerMask.NameToLayer("Ghost");
        ActiveForm.SetDead(true);

        if(!IsOwner) return;

        playerInventory.DropAll();
        playerInventory.Deselect();
        
        playerCharacter.SetSpectator(true);
        playerUI.hud.SetDead(true);

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(_ragdollNetworkId, out NetworkObject _ragdollObj))
        {
            Ragdoll _ragdoll = _ragdollObj.GetComponent<Ragdoll>();
            if (_ragdoll != null && _ragdoll.CameraTarget != null)
            {
                if (deathCamRoutine != null) StopCoroutine(deathCamRoutine);
                deathCamRoutine = StartCoroutine(DeathCam(_ragdoll.CameraTarget));
            }
        }
    }

    IEnumerator DeathCam(Transform _target)
    {
        deathCamTarget = _target;
        yield return new WaitForSeconds(deathCamDuration);
        deathCamTarget = null;
        deathCamRoutine = null;
    }

    void ClearDeathCam()
    {
        if (deathCamRoutine != null)
        {
            StopCoroutine(deathCamRoutine);
            deathCamRoutine = null;
        }
        deathCamTarget = null;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void RespawnClientRpc() {
        isDead = false;
        ClearDeathCam();

        playerCharacter.gameObject.layer = LayerMask.NameToLayer("Player");
        ActiveForm.SetDead(false);

        if(!IsOwner) return;

        playerCharacter.SetSpectator(false);
        playerUI.hud.SetDead(false);
    }

    [Rpc(SendTo.Owner)]
    public void RecieveForceClientRpc(Vector3 _force) {
        ActiveForm.AddForce(_force);
    }

    [Rpc(SendTo.Owner)]
    public void TeleportClientRpc(Vector3 _position) {
        ActiveForm.SetPosition(_position);
    }

    [Rpc(SendTo.Owner)]
    public void AddOrRemoveScoreboardItemClientRpc(bool _add, ulong _playerId, string _playerName, int _wins, int _kills, int _deaths) {
        if(_add) playerUI.scoreboard.AddItem(_playerId, _playerName, _wins, _kills, _deaths);
        else playerUI.scoreboard.RemoveItem(_playerId);
    }

    [Rpc(SendTo.Owner)]
    public void ScoreboardUpdateClientRpc(ulong _playerId, int _wins, int _kills, int _deaths) {
        playerUI.scoreboard.UpdateItem(_playerId, _wins, _kills, _deaths);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SetCrownClientRpc(bool _enabled) {
        playerUI.SetCrown(_enabled && !IsOwner);
    }

    [Rpc(SendTo.Owner)]
    public void AddKillfeedClientRpc(string _text, bool _clientIncluded) {
        playerUI.killfeed.AddKillfeedItem(_text, _clientIncluded);
    }

    [Rpc(SendTo.Owner)]
    public void ClearItemClientRpc(int _itemId = -1) {
        playerInventory.ClearItem(_itemId);
    }

    [Rpc(SendTo.Owner)]
    public void GiveItemClientRpc(ulong _itemNetworkId) {
        playerInventory.GiveItem(_itemNetworkId);
    }

}
