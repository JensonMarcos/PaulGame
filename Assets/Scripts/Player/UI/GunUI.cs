using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GunUI : MonoBehaviour
{
    [SerializeField] float speed;
    [SerializeField] TMP_Text ammoText;
    [SerializeField] RectTransform width, top, bottom;
    [SerializeField] GameObject sight, ammo;

    // [SerializeField] string reloadWord = "RELOADING";
    // [SerializeField] int reloadWindowSize = 3;

    Vector3 targetAmmoPos;

    public void Initialize(bool isOwner) {
        if(!isOwner) {
            sight.SetActive(false);
            ammo.SetActive(false);
        }
    }

    public void UpdateUI(ItemClient _item, bool aiming, bool scoped, float reloading) {
        ItemData _data = _item.data;

        if(_data.type is ItemType.Melee) {
            if(ammo.activeSelf) {
                sight.SetActive(false);
                ammo.SetActive(false);
            }
            return;
        } 
        
        if(!ammo.activeSelf) {
            sight.SetActive(true);
            ammo.SetActive(true);
        }

        if(sight.transform.localPosition != _data.sightPos) sight.transform.localPosition = _data.sightPos; //set sight pos

        sight.SetActive(aiming);
        ammo.SetActive(!scoped);
        
        width.sizeDelta = new Vector2(_data.WidTopBot.x , width.sizeDelta.y);
        top.sizeDelta = new Vector2(top.sizeDelta.x, _data.WidTopBot.y);
        bottom.sizeDelta = new Vector2(bottom.sizeDelta.x, _data.WidTopBot.z);

        //ammo pos when ADS/hip
        targetAmmoPos = aiming ? _data.ADSAmmoPos : _data.AmmoPos; 
        ammo.transform.localPosition = Vector3.Lerp(ammo.transform.localPosition, targetAmmoPos, speed * Time.deltaTime);
        ammo.transform.localEulerAngles = new Vector3(Mathf.Lerp(ammo.transform.localEulerAngles.x, aiming ? 0f : 15f, speed * Time.deltaTime), 0f, 0f);

        //update ammo text
        if(!PlayerManager.instance.damageEnabled.Value)
        {
            ammoText.text = "X";
            ammoText.color = Color.red;
            return;
        }

        if(reloading > 0) {
            int incrementAmmo = (int)(_data.ammoCap * (reloading/0.90f));

            ammoText.text = incrementAmmo.ToString();
            ammoText.color = Color.red;

            if(reloading > 0.925f) ammoText.text = " ";
        } else {
            ammoText.text = _item.Ammo.ToString();
            ammoText.color = (_item.Ammo < _item.data.ammoCap * 0.25) ? Color.red : Color.white;
        }
    }
}
