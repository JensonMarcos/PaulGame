using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] PlayerCharacter playerCharacter;
    [SerializeField] PlayerCamera playerCamera;

    void Start()
    {
        playerCamera.Initialize(playerCharacter.camTarget);
    }

    void Update()
    {
        //playerCharacter.HandleInputs();
        HandleCharacterInput();
        playerCamera.UpdateRotation();
        playerCamera.UpdatePosition(playerCharacter.camTarget);
    }

    private void HandleCharacterInput()
    {
        PlayerCharacterInputs characterInputs = new PlayerCharacterInputs();

        // Build the CharacterInputs struct
        characterInputs.MoveAxisForward = Input.GetAxisRaw("Vertical");
        characterInputs.MoveAxisRight = Input.GetAxisRaw("Horizontal");
        characterInputs.CameraRotation = playerCamera.transform.rotation;
        characterInputs.JumpDown = Input.GetKeyDown(KeyCode.Space);
        characterInputs.CrouchDown = Input.GetKeyDown(KeyCode.C);
        characterInputs.CrouchUp = Input.GetKeyUp(KeyCode.C);

        // Apply inputs to character
        playerCharacter.SetInputs(ref characterInputs);
    }
}
