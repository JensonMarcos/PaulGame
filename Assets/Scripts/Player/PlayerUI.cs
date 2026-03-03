using UnityEngine;
using UnityEngine.Rendering;

public class PlayerUI : MonoBehaviour
{
    public Scoreboard scoreboard;
    public Killfeed killfeed;
    [SerializeField] GunUI gunUI;

    [SerializeField] GameObject canvas;
    [SerializeField] Volume postProcessing;
    [SerializeField] GameObject crosshair;
    [SerializeField] GameObject scope;
    [SerializeField] float scopeThreshold = 0.99f;

    bool tabPressed, tabLastPressed;

    public void Initialize(bool isOwner)
    {
        gunUI.Initialize(isOwner);
        if(!isOwner)
        {
            canvas.SetActive(false);
            postProcessing.enabled = false;
            return;
        }
    }

    public void SetInputs(bool _tabPressed)
    {
        tabPressed = _tabPressed;
        if(tabPressed != tabLastPressed)
        {
            scoreboard.InputUpdate(tabPressed);
            tabLastPressed = tabPressed;
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
