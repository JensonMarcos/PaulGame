using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct PlayerState : INetworkSerializable
{
    [Header("Character")]
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;

    [Header("Combat")]
    //inventory stuff
    public int InventoryIndex;
    public float Aiming;
    public bool ReadyPull;
    public float Reloading;

    [Header("Animation")]
    public bool Melee;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Grounded);
        serializer.SerializeValue(ref Stance);
        serializer.SerializeValue(ref Velocity);

        serializer.SerializeValue(ref InventoryIndex);
        serializer.SerializeValue(ref Aiming);
        serializer.SerializeValue(ref ReadyPull);
        serializer.SerializeValue(ref Reloading);

        serializer.SerializeValue(ref Melee);
    }
}

public class Player : NetworkBehaviour
{
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

    //[SerializeField] ServerCollider serverCollider;

    bool isDead;
  
    public override void OnNetworkSpawn()
    {
        playerInputs = new PlayerInputs();
        playerInputs.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.camTarget, IsOwner);
        playerAnimations.Initialize();
        playerUI.Initialize(IsOwner);
        playerInventory.Initialize();

        //serverCollider.Initialize(IsServer && !IsOwner);

        if(!IsOwner)
        {
            playerCharacter.Motor.enabled = false;
            playerCharacter.gameObject.GetComponent<KinematicCharacterMotor>().enabled = false;
            //playerCharacter.gameObject.layer = LayerMask.NameToLayer("Ghost");
        }
    }

    public override void OnNetworkDespawn()
    {
        playerInputs.Dispose();
    }

    void Update()
    {
        if(!IsOwner && !isDead) playerState = NetworkPlayerState.Value;

        if(IsOwner)
        {
            HandleInputs();
            UpdateState();
        }

        if(!isDead)
        {
            playerAnimations.UpdateAnimatorValues(playerState);

            playerAnimations.UpdateAnimator(Time.deltaTime);
        }

        #if UNITY_EDITOR
        if(IsOwner && isDead && Keyboard.current.pKey.wasPressedThisFrame) {
            PlayerManager.instance.RespawnServerRpc(OwnerClientId);      
        }
        #endif
    }

    void LateUpdate()
    {
        int i = playerState.InventoryIndex;

        if(!isDead) playerAnimations.UpdateRigs(playerState, playerInventory.ClientInventory[i], playerCharacter.camTarget);

        playerCamera.UpdatePosition(playerCharacter.camTarget);

        if(IsOwner) {
            if(!isDead)
            {
                playerInventory.TryPickUp();
                playerCombat.UpdateCombat(playerState, playerInventory.ClientInventory[i]);
                playerCamera.UpdateCam(playerInventory.ClientInventory[i].data.adsZoom, playerState.Aiming);
            }

            playerUI.UpdateUI(playerState, playerInventory.ClientInventory[i]);

            UpdateState();
            NetworkPlayerState.Value = playerState;
        }
    }

    // void FixedUpdate()
    // {
    //     if(IsServer && !IsOwner) serverCollider.UpdateCollider(playerState.Stance, playerState.Velocity);
    // }

    void HandleInputs()
    {
        var inputs = playerInputs.Gameplay;

        Vector2 cameraInputs = inputs.Look.ReadValue<Vector2>();
        playerCamera.UpdateRotation(cameraInputs, playerInventory.ClientInventory[playerState.InventoryIndex].data);
        

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
            Scroll = inputs.Scroll.ReadValue<float>(),
            NumKeys = (int)inputs.NumKeys.ReadValue<float>()-1
        };
        playerInventory.SetInputs(inventoryInputs);


        bool _auto = playerInventory.ClientInventory[playerInventory.InvIndex].data.isAutomatic;
        CombatInputs combatInputs = new CombatInputs {
            Attack = _auto ? inputs.Attack.IsPressed() : inputs.Attack.WasPressedThisFrame(),
            Aim = inputs.Aim.IsPressed(),
            Reload = inputs.Reload.WasPressedThisFrame()
        };
        playerCombat.SetInputs(combatInputs, playerState.Stance is Stance.Sprint, playerInventory.ReadyPull);   
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

        playerState.Melee = playerInventory.ClientInventory[playerState.InventoryIndex].data.type == ItemType.Melee;
    }

    [ClientRpc]
    public void DieClientRpc() {
        isDead = true;
        //if(IsServer) serverCollider.gameObject.layer = LayerMask.NameToLayer("Ghost");

        if(!IsOwner) {
            playerCharacter.gameObject.SetActive(false);
            return;
        }

        playerAnimations.SetAnimationActive(false);
        playerInventory.DropAll();

        playerCharacter.gameObject.layer = LayerMask.NameToLayer("Ghost");
        playerCharacter.SetSpectator(true);

    }

    [Rpc(SendTo.ClientsAndHost)]
    public void RespawnClientRpc() {
        isDead = false;
        //if(IsServer) serverCollider.gameObject.layer = LayerMask.NameToLayer("Player");

        if(!IsOwner) {
            playerCharacter.gameObject.SetActive(true);
            return;
        } 

        playerAnimations.SetAnimationActive(true);
        playerCharacter.gameObject.layer = LayerMask.NameToLayer("Player");
        playerCharacter.SetSpectator(false);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void RecieveForceClientRpc(Vector3 force) {
        playerCharacter.AddForce(force);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void TeleportClientRpc(Vector3 position) {
        if(IsOwner) playerCharacter.SetPosition(position);
    }
}
