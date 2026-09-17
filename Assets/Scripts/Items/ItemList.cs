using UnityEngine;

[System.Serializable]
public struct ItemElement
{
    public GameObject item;
    public int id;
    public float weight;
} 

[CreateAssetMenu(fileName = "ItemList", menuName = "Scriptable Objects/ItemList")]
public class ItemList : ScriptableObject
{
    public ItemElement[] itemPool;
    public ItemElement[] specialItems;

    public GameObject GetItem(int id)
    {
        for(int i = 0; i < itemPool.Length; i++) {
            if(itemPool[i].id == id) return itemPool[i].item;
        }

        for(int i = 0; i < specialItems.Length; i++) {
            if(specialItems[i].id == id) return specialItems[i].item;
        }

        return null;
    }

    public int GetItemId(GameObject itemInstance)
    {
        if (itemInstance == null) return -1;
        Item item = itemInstance.GetComponent<Item>();
        if (item == null) return -1;
        return GetItemIdByClientPrefab(item.clientPrefab);
    }

    public int GetItemIdByClientPrefab(GameObject clientPrefab)
    {
        if (clientPrefab == null) return -1;

        for(int i = 0; i < itemPool.Length; i++) {
            Item item = itemPool[i].item != null ? itemPool[i].item.GetComponent<Item>() : null;
            if(item != null && item.clientPrefab == clientPrefab) return itemPool[i].id;
        }
        for(int i = 0; i < specialItems.Length; i++) {
            Item item = specialItems[i].item != null ? specialItems[i].item.GetComponent<Item>() : null;
            if(item != null && item.clientPrefab == clientPrefab) return specialItems[i].id;
        }
        return -1;
    }

    public int GetRandomItemId()
    {
        float totalWeight = 0f;
        for(int i = 0; i < itemPool.Length; i++) {
            totalWeight += itemPool[i].weight;
        }

        float randomWeight = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        for(int i = 0; i < itemPool.Length; i++) {
            cumulativeWeight += itemPool[i].weight;
            if(randomWeight <= cumulativeWeight) {
                return itemPool[i].id;
            }
        }

        return -1;
    }
}
