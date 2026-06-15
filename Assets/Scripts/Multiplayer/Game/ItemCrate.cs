using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ItemCrate : NetworkBehaviour
{
    [Header("Loot")]
    public ItemList itemList;
    [SerializeField] int itemCount = 1;

    readonly List<int> drops = new();
    bool broken;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (itemList == null) itemList = GameManager.instance.itemList;

        // Decide the loot up front so it is the same regardless of who breaks it.
        drops.Clear();
        for (int i = 0; i < itemCount; i++)
            drops.Add(itemList.GetRandomItemId());
    }

    [Rpc(SendTo.Server)]
    public void BreakCrateServerRpc()
    {
        BreakCrate();
    }

    public void BreakCrate()
    {
        if (!IsServer || broken) return;
        broken = true;

        Vector3 pos = transform.position;

        foreach (int id in drops)
        {
            GameObject prefab = itemList.GetItem(id);
            if (prefab == null) continue;

            if(GameManager.instance != null)
                GameManager.instance.SpawnItem(prefab, pos);
            else
            {
                GameObject item = Instantiate(prefab, pos, Quaternion.identity);
                item.GetComponent<NetworkObject>().Spawn(true);
            }
        }

        if(GameManager.instance != null)
            GameManager.instance.RemoveObject(gameObject);
        else
            GetComponent<NetworkObject>().Despawn(true);
    }
}
