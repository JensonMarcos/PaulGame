using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ItemCrate : NetworkBehaviour
{
    [Header("Loot")]
    public ItemList itemList;
    [SerializeField] int itemCount;
    bool broken;
    GameManager gameManager;
    public Room room;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        gameManager = GameManager.instance;
        if (itemList == null) itemList = gameManager.itemList;

        itemCount = (int)(Random.Range(1f, 2f) + PlayerManager.instance.Players.Count * 0.2f);
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

        if (room == null || room.crateLootEnabled)
        {
            for (int i=0; i<itemCount; i++)
            {
                int id = itemList.GetRandomItemId();

                GameObject prefab = itemList.GetItem(id);
                if (prefab == null) continue;

                if(gameManager != null)
                    gameManager.SpawnItem(prefab, transform.position);
                else
                {
                    GameObject item = Instantiate(prefab, transform.position, Quaternion.identity);
                    item.GetComponent<NetworkObject>().Spawn(true);
                }
            }
        }

        if(GameManager.instance != null)
            gameManager.RemoveObject(gameObject);
        else
            GetComponent<NetworkObject>().Despawn(true);
    }
}
