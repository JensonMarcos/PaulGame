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
    int lastAmmo = -100;
    Color lastAmmoColor;

    public void Initialize(bool isOwner) {
        if(!isOwner) {
            sight.SetActive(false);
            ammo.SetActive(false);
        }
    }

    public void UpdateUI(ItemClient _item, bool aiming, bool scoped, float reloading) {
        if(!_item.ShowsHudAmmo) {
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

        if(sight.transform.localPosition != _item.SightPos) sight.transform.localPosition = _item.SightPos;

        sight.SetActive(aiming);
        ammo.SetActive(!scoped);
        
        width.sizeDelta = new Vector2(_item.WidTopBot.x , width.sizeDelta.y);
        top.sizeDelta = new Vector2(top.sizeDelta.x, _item.WidTopBot.y);
        bottom.sizeDelta = new Vector2(bottom.sizeDelta.x, _item.WidTopBot.z);

        targetAmmoPos = aiming ? _item.AdsAmmoPos : _item.AmmoPos; 
        ammo.transform.localPosition = Vector3.Lerp(ammo.transform.localPosition, targetAmmoPos, speed * Time.deltaTime);
        ammo.transform.localEulerAngles = new Vector3(Mathf.Lerp(ammo.transform.localEulerAngles.x, aiming ? 0f : 15f, speed * Time.deltaTime), 0f, 0f);

        if(!PlayerManager.instance.damageEnabled.Value)
        {
            SetAmmo(-1, Color.red);
            return;
        }

        if(reloading > 0) {
            if(reloading > 0.925f)
                SetAmmo(-2, Color.red);
            else
                SetAmmo((int)(_item.AmmoCap * (reloading/0.90f)), Color.red);
        } else {
            SetAmmo(_item.Ammo, (_item.Ammo < _item.AmmoCap * 0.25) ? Color.red : Color.white);
        }
    }

    void SetAmmo(int ammo, Color color)
    {
        if(ammo != lastAmmo)
        {
            lastAmmo = ammo;
            ammoText.text = ammo == -1 ? "X" : ammo == -2 ? " " : ammo.ToString();
        }
        if(color != lastAmmoColor)
        {
            lastAmmoColor = color;
            ammoText.color = color;
        }
    }
}
