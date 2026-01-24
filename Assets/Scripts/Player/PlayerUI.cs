using UnityEngine;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] GameObject canvas;
    [SerializeField] GunUI gunUI;
    [SerializeField] GameObject crosshair;
    [SerializeField] GameObject scope;
    [SerializeField] float scopeThreshold = 0.99f;

    public void Initialize(bool isOwner)
    {
        gunUI.Initialize(isOwner);
        if(!isOwner)
        {
            canvas.SetActive(false);
            return;
        }
    }

    public void UpdateUI(PlayerState _state, ItemClient _item)
    {
        float aiming = _state.Aiming;
        bool scoped = false;

        if(_item.data.type is ItemType.Sniper)
        {
            scoped = aiming > scopeThreshold;
            scope.SetActive(scoped);
            crosshair.SetActive(!scoped);
            _item.model.SetActive(!scoped);

        } else if(scope.activeSelf)
        {
            scope.SetActive(false);
            crosshair.SetActive(true);
            if(_item.model != null) _item.model.SetActive(true);
        } 

        gunUI.UpdateUI(_item, aiming > 0.5f, scoped, _state.Reloading);
    }
}
