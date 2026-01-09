using UnityEngine;

public interface IItemAction
{
    void OnLeftClick();
    void OnRightClick();
}

[System.Serializable]
public class ItemClient : MonoBehaviour
{
    public ItemData data;
    public Transform LHand, RHand;
    public Transform sight, muzzleTrans;

    public IItemAction action;

    public int Ammo;

    void Start()
    {
        action = GetComponent<IItemAction>();
    }

    public void LeftClick()
    {
        if(action == null) return;
        action.OnLeftClick();
    }

    public void RightClick()
    {
        if(action == null) return;
        action.OnRightClick();
    }
}



