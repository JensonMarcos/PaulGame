using UnityEngine;

public abstract class ItemAction : MonoBehaviour
{
    public virtual void OnLeftClick(PlayerState state, Player player, bool isOwner) { }
    public virtual void OnRightClick(PlayerState state, Player player, bool isOwner) { }
    public virtual void OnHit(PlayerState state, Player player, bool isOwner, ulong targetId) { }
    public virtual void PlayAttack() { }
}

[System.Serializable]
public class ItemClient : MonoBehaviour
{
    public ItemData data;
    public GameObject model;
    public Transform LHand, RHand;
    public Transform sight, muzzleTrans;

    public ItemAction action;

    public int Ammo;

    void Start()
    {
        action = GetComponent<ItemAction>();
    }

    public void LeftClick(PlayerState state, Player player, bool isOwner)
    {
        if(action == null) return;
        action.OnLeftClick(state, player, isOwner);
    }

    public void RightClick(PlayerState state, Player player, bool isOwner)
    {
        if(action == null) return;
        action.OnRightClick(state, player, isOwner);
    }

    public void OnHit(PlayerState state, Player player, bool isOwner, ulong targetId)
    {
        if(action == null) return;
        action.OnHit(state, player, isOwner, targetId);
    }

    public void PlayAttack()
    {
        if(action == null) return;
        action.PlayAttack();
    }
}
