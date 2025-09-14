using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;

public class GunUI : NetworkBehaviour
{
    [SerializeField] Shooting player;
    [SerializeField] float speed;
    [SerializeField] TMP_Text ammoText;
    [SerializeField] RectTransform width, top, bottom;
    public GameObject Sight, Ammo;
    Item activeItem;
    Vector3 targetAmmoPos;

    void Start() {
        if(!IsOwner) {
            Sight.SetActive(false);
            Ammo.SetActive(false);
        }
    }

    void Update() {
        if(!IsOwner) return;

        if(player.item == null) {
            if(Ammo.activeSelf) {
                Sight.SetActive(false);
                Ammo.SetActive(false);
            }
            return;
        } 

        if(activeItem != player.item) activeItem = player.item; //set gun script

        if(activeItem.data.type == 2) { //if melee
            if(Ammo.activeSelf) {
                Sight.SetActive(false);
                Ammo.SetActive(false);
            }
            return;
        }

        if(!Ammo.activeSelf) {
            Sight.SetActive(true);
            Ammo.SetActive(true);
        }

        if(Sight.transform.localPosition != activeItem.data.sightPos) Sight.transform.localPosition = activeItem.data.sightPos; //set sight pos

        if(!activeItem.data.sniper) Sight.SetActive(player.isScoping);
        else if(activeItem.data.sniper) Sight.SetActive(false);  //show sight when ADS
        
        width.sizeDelta = new Vector2(activeItem.data.WidTopBot.x , width.sizeDelta.y);
        top.sizeDelta = new Vector2(top.sizeDelta.x, activeItem.data.WidTopBot.y);
        bottom.sizeDelta = new Vector2(bottom.sizeDelta.x, activeItem.data.WidTopBot.z);

        //ammo pos when ADS/hip
        targetAmmoPos = player.isScoping ? activeItem.data.ADSAmmoPos : activeItem.data.AmmoPos; 
        Ammo.transform.localPosition = Vector3.Lerp(Ammo.transform.localPosition, targetAmmoPos, speed * Time.deltaTime);
        Ammo.transform.localEulerAngles = new Vector3(Mathf.Lerp(Ammo.transform.localEulerAngles.x, player.isScoping ? 0f : 15f, speed * Time.deltaTime), 0f, 0f);

        ammoText.text = activeItem.ammo + "";
        ammoText.color = (activeItem.ammo < activeItem.data.ammoCap * 0.25) ? Color.red : Color.white;
    }
}
