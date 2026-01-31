using KinematicCharacterController;
using Unity.Netcode;
using UnityEngine;

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

public struct PlayerInputs
{
    public float ForwardAxis;
    public float RightAxis;
    public Quaternion CameraRotation;
    public bool Jump;
    public bool Crouch;
    public bool Sprint;

    public bool Attack;
    public bool Aim;
    public bool Reload;
    public bool Interact;
    public bool Drop;

    public float ScrollWheel;
    public int NumKey;

}

public class Player : NetworkBehaviour
{
    NetworkVariable<PlayerState> NetworkPlayerState = new NetworkVariable<PlayerState>(
        writePerm: NetworkVariableWritePermission.Owner,
        readPerm: NetworkVariableReadPermission.Everyone
    );
    
    public PlayerState playerState;
    [SerializeField] PlayerInputs inputs;

    public PlayerInventory playerInventory;
    public PlayerCharacter playerCharacter;
    [SerializeField] PlayerCamera playerCamera;
    [SerializeField] PlayerAnimations playerAnimations;
    [SerializeField] PlayerCombat playerCombat;
    [SerializeField] PlayerUI playerUI;

    //[SerializeField] ServerCollider serverCollider;

    bool isDead;
  
    void Start()
    {
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

    void Update()
    {
        if(!IsOwner && !isDead) playerState = NetworkPlayerState.Value;

        if(IsOwner)
        {
            playerCamera.UpdateRotation(playerInventory.ClientInventory[playerState.InventoryIndex].data);

            UpdateState();
            HandleInputs();
            UpdateState();
        }

        if(!isDead)
        {
            playerAnimations.UpdateAnimatorValues(playerState);

            playerAnimations.UpdateAnimator(Time.deltaTime);
        }


        if(IsOwner && isDead && Input.GetKeyDown(KeyCode.P)) {
            PlayerManager.instance.RespawnServerRpc(OwnerClientId);      
        }
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
        inputs.ForwardAxis = Input.GetAxisRaw("Vertical");
        inputs.RightAxis = Input.GetAxisRaw("Horizontal");
        inputs.CameraRotation = playerCamera.transform.rotation;
        inputs.Jump = Input.GetKeyDown(KeyCode.Space);
        inputs.Crouch = Input.GetKey(KeyCode.LeftControl);
        inputs.Sprint = Input.GetKey(KeyCode.LeftShift);

        inputs.Attack = Input.GetKey(KeyCode.Mouse0);
        inputs.Aim = Input.GetKey(KeyCode.Mouse1);
        inputs.Reload = Input.GetKeyDown(KeyCode.R);
        inputs.Interact = Input.GetKeyDown(KeyCode.E);
        inputs.Drop = Input.GetKeyDown(KeyCode.G);

        inputs.ScrollWheel = Input.GetAxis("Mouse ScrollWheel");

        inputs.NumKey = -1;
        for (int i = 0; i < 9; i++) //num keys
        {
            if (Input.GetKeyDown((i + 1).ToString()))
            {
                inputs.NumKey = i;
            }
        }

        playerInventory.SetInputs(inputs, playerState.Velocity);
        playerCharacter.SetInputs(inputs);
        playerCombat.SetInputs(inputs, playerState.Stance is Stance.Sprint, playerInventory.ReadyPull, playerInventory.ClientInventory[playerInventory.InvIndex].data.isAutomatic);   
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
