using UnityEngine;
using TMPro;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Unity.Netcode;


public class Health : NetworkBehaviour
{
    public float health = 100f;//, regenTime;
    [SerializeField] TextMeshProUGUI healthText;
    //float timeTillRegen;
    public GameObject firstPerson, thirdPerson, spectator;
    public bool dead = false;
    [SerializeField] Aiming Aim;
    [SerializeField] SpectatorMovement specAim;


    // public Volume PP;
    // private float redTime;
    // private FilmGrain grain;
    // private ColorAdjustments color;
    // private Vignette vignette;
    // [SerializeField] private Image bloodScreen, red;


    void Start() {
        // PP.profile.TryGet<FilmGrain>(out grain);
        // PP.profile.TryGet<ColorAdjustments>(out color);
        // PP.profile.TryGet<Vignette>(out vignette);
        healthText.text = health.ToString();
    }

    void Update() {
        if(!IsOwner) return;

        if(Input.GetKeyDown(KeyCode.P)) {
            PlayerManager.instance.RespawnServerRpc(OwnerClientId);
        }

        // if(Input.GetKeyDown(KeyCode.O)) {
        //     PlayerManager.instance.DealDamageServerRpc(GetComponent<NetworkObject>().OwnerClientId, 1000f);
        // }

        //make health look good
        // bloodScreen.rectTransform.sizeDelta = new Vector2(192f * ((health*1.5f+300)/100), 108f * ((health*1.5f+300)/100));
        // bloodScreen.color = new Color(bloodScreen.color.r, bloodScreen.color.g, bloodScreen.color.b, 1 - (health-10)/80);
        // red.color = new Color(red.color.r, red.color.g, red.color.b, redTime);
        // color.saturation.value = Mathf.Clamp(health/0.25f - 160f, -100f, 0f);
        // grain.intensity.value = Mathf.Clamp(health/-25f + 1.6f, 0f, 1f);
        // vignette.intensity.value = Mathf.Clamp(health/-200f + 0.5f, 0.35f, 0.5f);
        
        // if(health > 0 && health < 100 && Time.time >= timeTillRegen) {
        //     health += Time.deltaTime * 20f;
        // }
        // if(health > 100) health = 100;

        //if(redTime > 0) redTime -= Time.deltaTime;

        //if(transform.position.y < -50) PlayerManager.instance.PV.RPC("RPC_Damage", RpcTarget.MasterClient, PV.Owner, 100000f);
    }

    [ClientRpc]
    public void UpdateHealthClientRpc(float _health, ClientRpcParams clientRpcParams = default) {
        if(!IsOwner) return;
        health = _health;
        healthText.text = health.ToString();
        // if(health <= 0) {
        //     print("dead");
        //     GetComponent<Inventory>().DropAll();
        //     GetComponent<MovementController>().Die();
        // }
    }

    [ClientRpc]
    public void DieClientRpc(Vector3 teleport, ClientRpcParams clientRpcParams = default) {
        dead = true;
        if(!IsOwner) {
            thirdPerson.SetActive(false);
            GetComponent<BoxCollider>().enabled = false;
            return;
        } 

        print("dead");
        GetComponent<Inventory>().DropAll();
        //GetComponent<MovementController>().Die();
        // GetComponent<BoxCollider>().includeLayers = 0 << 1;
        // GetComponent<Rigidbody>().includeLayers = 0 << 1;
        gameObject.layer = LayerMask.NameToLayer("Ghost");
        GetComponent<MovementController>().groundLayer = GetComponent<MovementController>().spectatorLayer;
        specAim.realRotation = Aim.realRotation;
        firstPerson.SetActive(false);
        thirdPerson.SetActive(false);
        spectator.SetActive(true);
        GetComponent<Inventory>().enabled = false;
        GetComponent<Shooting>().enabled = false;

        if(teleport != Vector3.zero) {
            transform.position = teleport;
        } 
    }

    // [ServerRpc(RequireOwnership = false)]
    // public void RespawnServerRpc(){
    //     if(dead) {
    //         int i = PlayerManager.instance.allplayers.FindIndex(x => x.ID == GetComponent<NetworkObject>().OwnerClientId);
    //         PlayerManager.instance.allplayers[i].isDead = false;
    //         PlayerManager.instance.allplayers[i].health = 100f;
    //         dead = false;
    //         RespawnClientRpc();
    //     } 
    // }

    [ClientRpc]
    public void RespawnClientRpc() {
        
        if(!IsOwner) {
            thirdPerson.SetActive(true);
            GetComponent<BoxCollider>().enabled = true;
            return;
        } 
        dead = false;
        spectator.SetActive(false);
        health = 100f;
        healthText.text = health.ToString();
        firstPerson.SetActive(true);
        Aim.realRotation = specAim.realRotation;
        gameObject.layer = LayerMask.NameToLayer("PlayerHitbox");
        // GetComponent<BoxCollider>().includeLayers = 0;
        // GetComponent<Rigidbody>().includeLayers = 0;
        GetComponent<MovementController>().groundLayer = GetComponent<MovementController>().normalLayer;
        GetComponent<Inventory>().enabled = true;
        GetComponent<Shooting>().enabled = true;
    }


}
