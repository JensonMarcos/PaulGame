using UnityEngine;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] GameObject canvas;
    [SerializeField] GunUI gunUI;

    public void Initialize(bool isOwner)
    {
        gunUI.Initialize(isOwner);
        if(!isOwner)
        {
            canvas.SetActive(false);
            return;
        }
    }

    public void UpdateUI(ItemClient _item, bool aiming)
    {
        gunUI.UpdateUI(_item, aiming);
    }
}
