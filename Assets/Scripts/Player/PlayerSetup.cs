using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    [SerializeField] GameObject firstPersonObject, thirdPersonObject;
    [SerializeField] Aiming aiming;
    [SerializeField] GunAnimation gunAnim;
    [SerializeField] CamAnimation camAnim;
    [SerializeField] MovementController movementController;
    [SerializeField] AimPoint aimPoint;
    [SerializeField] ScuffedFeetAnimation feet;

    void Start()
    {
        if (!IsOwner)
        { //other clients
            GetComponent<MovementController>().enabled = false;
            aiming.enabled = false;
            firstPersonObject.SetActive(false);
            thirdPersonObject.SetActive(true);
            gunAnim.enabled = false;
            camAnim.enabled = false;
            movementController.enabled = false;
            aimPoint.enabled = false;
            feet.enabled = false;
        }
        else
        { //owner
            thirdPersonObject.SetActive(false);
        }
        if(!GetComponent<NetworkObject>().IsPlayerObject) Destroy(gameObject);
    }
}
