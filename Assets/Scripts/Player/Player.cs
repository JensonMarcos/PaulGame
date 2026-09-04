using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct PlayerState : INetworkSerializable, System.IEquatable<PlayerState>
{
    [Header("Character")]
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;

    [Header("Combat")]
    public int InventoryIndex;
    public float Aiming;
    public bool ReadyPull;
    public float Reloading;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Grounded);
        serializer.SerializeValue(ref Stance);

        byte inventoryIndex = (byte)InventoryIndex;
        byte aiming = Compress01(Aiming);
        byte reloading = Compress01(Reloading);
        serializer.SerializeValue(ref inventoryIndex);
        serializer.SerializeValue(ref aiming);
        serializer.SerializeValue(ref ReadyPull);
        serializer.SerializeValue(ref reloading);

        if (serializer.IsReader)
        {
            InventoryIndex = inventoryIndex;
            Aiming = Decompress01(aiming);
            Reloading = Decompress01(reloading);
        }
    }

    public bool Equals(PlayerState other)
    {
        return Grounded == other.Grounded
            && Stance == other.Stance
            && InventoryIndex == other.InventoryIndex
            && Compress01(Aiming) == Compress01(other.Aiming)
            && ReadyPull == other.ReadyPull
            && Compress01(Reloading) == Compress01(other.Reloading);
    }

    static byte Compress01(float value)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
    }

    static float Decompress01(byte value)
    {
        return value / 255f;
    }
}

public class Player : NetworkBehaviour
{
    // Capsules of players this machine doesn't own, for locally simulated props to ignore.
    public static readonly List<Collider> RemoteColliders = new List<Collider>();
    public static event System.Action<Collider> RemoteColliderAdded;

    NetworkVariable<PlayerState> NetworkPlayerState = new NetworkVariable<PlayerState>(
        writePerm: NetworkVariableWritePermission.Owner,
        readPerm: NetworkVariableReadPermission.Everyone
    );
    
    public PlayerState playerState;

    PlayerInputs playerInputs;

    public PlayerInventory playerInventory;
    public PlayerCharacter playerCharacter;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] PlayerAnimations playerAnimations;
    [SerializeField] PlayerCombat playerCombat;
    [SerializeField] PlayerUI playerUI;
    [SerializeField] float deathCamDuration = 3f;

    bool isDead;
    Transform deathCamTarget;
    Coroutine deathCamRoutine;
    Vector3 lastRemotePosition;
  
    public override void OnNetworkSpawn()
    {
        playerInputs = new PlayerInputs();
        playerInputs.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.camTarget, IsOwner);
        playerAnimations.Initialize();
        playerUI.Initialize(IsOwner, OwnerClientId);
        playerInventory.Initialize();

        if(IsOwner && GameManager.instance != null)
        {
            GameManager.instance.GameTitle.OnValueChanged += playerUI.hud.OnTitleChanged;
        }

        if(!IsOwner)
        {
            playerCharacter.Motor.enabled = false;
            RegisterRemoteCollider();
        }

        lastRemotePosition = playerCharacter.transform.position;
    }

    public override void OnNetworkDespawn()
    {
        if(IsOwner && GameManager.instance != null)
            GameManager.instance.GameTitle.OnValueChanged -= playerUI.hud.OnTitleChanged;

        if(!IsOwner) RemoteColliders.Remove(playerCharacter.Motor.Capsule);

        playerInputs.Dispose();
    }

    void RegisterRemoteCollider()
    {
        Collider capsule = playerCharacter.Motor.Capsule;
        if(capsule == null || RemoteColliders.Contains(capsule)) return;

        RemoteColliders.Add(capsule);
        RemoteColliderAdded?.Invoke(capsule);
    }

    void Update()
    {
        if(!IsOwner && !isDead)
        {
            playerState = NetworkPlayerState.Value;
            Vector3 pos = playerCharacter.transform.position;
            if (Time.deltaTime > 0f)
                playerState.Velocity = (pos - lastRemotePosition) / Time.deltaTime;
            lastRemotePosition = pos;
        }

        if(IsOwner)
        {
            HandleInputs();
        }

        if(!isDead)
        {
            playerAnimations.UpdateAnimatorValues(playerState, playerInventory.ClientInventory[playerState.InventoryIndex].data);

            playerAnimations.UpdateAnimator(Time.deltaTime);
        }

        #if UNITY_EDITOR
        if(IsOwner && isDead && Keyboard.current.pKey.wasPressedThisFrame) {
            PlayerManager.instance.DEBUGRespawnServerRpc(OwnerClientId);      
        }
        #endif
    }

    void LateUpdate()
    {
        //single state refresh per frame, after the character motor has moved
        if(IsOwner) UpdateState();
        else if(!isDead) playerCharacter.SetYawFromCamera(playerCamera.transform.rotation);

        int i = playerState.InventoryIndex;

        if(!isDead) playerAnimations.UpdateRigs(playerState, playerInventory.ClientInventory[i], playerCharacter.camTarget);

        Transform camFollow = deathCamTarget != null ? deathCamTarget : playerCharacter.camTarget;
        playerCamera.UpdatePosition(camFollow);
        if (deathCamTarget != null)
            playerCamera.UpdateDeathCamRotation(deathCamTarget);

        if(IsOwner) {
            if(!isDead)
            {
                playerInventory.TryPickUp();
                playerCombat.UpdateCombat(playerState, playerInventory.ClientInventory[i]);
                playerCamera.UpdateCam(playerInventory.ClientInventory[i].data.adsZoom, playerState.Aiming);
            }

            playerUI.UpdateUI(playerState, playerInventory.ClientInventory[i]);

            //only mark the network variable dirty when the state actually changed
            if(!playerState.Equals(NetworkPlayerState.Value)) NetworkPlayerState.Value = playerState;
        }
    }


    void HandleInputs()
    {
        var inputs = playerInputs.Gameplay;

        if (deathCamTarget == null)
        {
            Vector2 cameraInputs = inputs.Look.ReadValue<Vector2>();
            playerCamera.UpdateRotation(cameraInputs, playerInventory.ClientInventory[playerState.InventoryIndex].data);
        }

        CharacterInputs characterInputs = new CharacterInputs {
            ForwardAxis = inputs.Move.ReadValue<Vector2>().y,
            RightAxis = inputs.Move.ReadValue<Vector2>().x,
            CameraRotation = playerCamera.transform.rotation,
            Jump = inputs.Jump.WasPressedThisFrame(),
            Crouch = inputs.Crouch.IsPressed(),
            Sprint = inputs.Sprint.IsPressed()
        };
        playerCharacter.SetInputs(characterInputs);

        InventoryInputs inventoryInputs = new InventoryInputs {
            Interact = inputs.Interact.WasPressedThisFrame(),
            Drop = inputs.Drop.WasPressedThisFrame(),
            Velocity = playerCharacter.State.Velocity,
            //Scroll = inputs.Scroll.ReadValue<float>(),
            //NumKeys = (int)inputs.NumKeys.ReadValue<float>()-1
        };
        playerInventory.SetInputs(inventoryInputs);


        bool _auto = playerInventory.ClientInventory[playerInventory.InvIndex].data.isAutomatic;
        CombatInputs combatInputs = new CombatInputs {
            Attack = _auto ? inputs.Attack.IsPressed() : inputs.Attack.WasPressedThisFrame(),
            Aim = inputs.Aim.IsPressed(),
            Reload = PlayerManager.instance.reloadEnabled.Value ? inputs.Reload.WasPressedThisFrame() : false
        };
        playerCombat.SetInputs(combatInputs, playerState.Stance is Stance.Sprint, playerInventory.ReadyPull);   

        playerUI.SetInputs(inputs.Tab.IsPressed());
    }

    void UpdateState()
    {
        CharacterState _characterState = playerCharacter.State;
        playerState.Grounded = _characterState.Grounded;
        playerState.Stance = _characterState.Stance;
        playerState.Velocity = _characterState.Velocity;

        playerState.InventoryIndex = playerInventory.InvIndex;
        playerState.Aiming = playerCombat.Aiming;
        if(playerInventory.ClientInventory[playerState.InventoryIndex].data.type == ItemType.Melee) playerState.Aiming = 0;
        playerState.ReadyPull = playerInventory.ReadyPull;
        playerState.Reloading = playerCombat.Reloading;

        // playerState.Melee = playerInventory.ClientInventory[playerState.InventoryIndex].data.type == ItemType.Melee;
    }

    [Rpc(SendTo.Owner)]
    public void UpdateHealthClientRpc(float health) {
        playerUI.hud.UpdateHealth(health);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DieClientRpc(ulong ragdollNetworkId) {
        isDead = true;

        playerCharacter.gameObject.layer = LayerMask.NameToLayer("Ghost");

        if(!IsOwner) {
            playerCharacter.root.gameObject.SetActive(false);
            return;
        }

        playerInventory.DropAll();
        playerInventory.Deselect();
        
        playerAnimations.SetAnimationActive(false);
        playerCharacter.SetSpectator(true);
        playerUI.hud.SetDead(true);

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(ragdollNetworkId, out NetworkObject ragdollObj))
        {
            Ragdoll ragdoll = ragdollObj.GetComponent<Ragdoll>();
            if (ragdoll != null && ragdoll.CameraTarget != null)
            {
                if (deathCamRoutine != null) StopCoroutine(deathCamRoutine);
                deathCamRoutine = StartCoroutine(DeathCam(ragdoll.CameraTarget));
            }
        }
    }

    IEnumerator DeathCam(Transform target)
    {
        deathCamTarget = target;
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

        if(!IsOwner) {
            lastRemotePosition = playerCharacter.transform.position;
            playerCharacter.root.gameObject.SetActive(true);
            return;
        } 

        playerAnimations.SetAnimationActive(true);
        playerCharacter.SetSpectator(false);
        playerUI.hud.SetDead(false);
    }

    [Rpc(SendTo.Owner)]
    public void RecieveForceClientRpc(Vector3 force) {
        playerCharacter.AddForce(force);
    }

    [Rpc(SendTo.Owner)]
    public void TeleportClientRpc(Vector3 position) {
        playerCharacter.SetPosition(position);
    }

    [Rpc(SendTo.Owner)]
    public void AddOrRemoveScoreboardItemClientRpc(bool add, ulong playerId, string playerName, int wins, int kills, int deaths) {
        if(add) playerUI.scoreboard.AddItem(playerId, playerName, wins, kills, deaths);
        else playerUI.scoreboard.RemoveItem(playerId);
    }

    [Rpc(SendTo.Owner)]
    public void ScoreboardUpdateClientRpc(ulong playerId, int wins, int kills, int deaths) {
        playerUI.scoreboard.UpdateItem(playerId, wins, kills, deaths);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SetCrownClientRpc(bool enabled) {
        playerUI.SetCrown(enabled && !IsOwner);
    }

    [Rpc(SendTo.Owner)]
    public void AddKillfeedClientRpc(string text, bool clientIncluded) {
        playerUI.killfeed.AddKillfeedItem(text, clientIncluded);
    }

    [Rpc(SendTo.Owner)]
    public void ClearItemClientRpc(int itemId = -1) {
        playerInventory.ClearItem(itemId);
    }

    [Rpc(SendTo.Owner)]
    public void GiveItemClientRpc(ulong itemNetworkId) {
        playerInventory.GiveItem(itemNetworkId);
    }

    public void HandPush(Vector3 force) {
        playerAnimations.HandPushServerRpc(force);
        playerAnimations.HandPush(force);
    }

    public void TriggerAnimation(string name) {
        playerAnimations.TriggerAnimationServerRpc(name);
        playerAnimations.TriggerAnimation(name);
    }

    public void CallItemAction(bool rightClick) {
        CallItemActionServerRpc(rightClick);
        LocalCallItemAction(rightClick);
    }

    [Rpc(SendTo.Server)]
    void CallItemActionServerRpc(bool rightClick, RpcParams rpcParams = default) {
        CallItemActionClientRpc(rightClick, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void CallItemActionClientRpc(bool rightClick, RpcParams rpcParams = default) {
        LocalCallItemAction(rightClick);
    }

    void LocalCallItemAction(bool rightClick) {
        ItemClient item = playerInventory.ClientInventory[playerState.InventoryIndex];
        if(rightClick) item.RightClick();
        else item.LeftClick();
    }
}
