using UnityEngine;

public class C4Weapon : SwingWeapon
{
    protected override void OnPlayerHit(ulong targetId)
    {
        if (GameManager.instance == null) return;

        int itemId = GameManager.instance.itemList.GetItemIdByClientPrefab(PrefabAsset);
        if (itemId == -1) return;

        PlayerManager.instance.SwapItemServerRpc(targetId, itemId);
    }
}
