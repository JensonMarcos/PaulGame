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
