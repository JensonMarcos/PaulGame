using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public Item[] Inventory = new Item[3];
    public ItemClient[] ClientInventory = new ItemClient[3];

    public int InvIndex;
    public bool ReadyPull;
    public bool Reloading;

    [SerializeField] GameObject HandsData;

    [SerializeField] PlayerAnimations animations;



    public void SetInputs(PlayerInputs _inputs)
    {
        int prevIndex = InvIndex;
        if (_inputs.ScrollWheel < 0f)
        { //scrollwheel
            if (InvIndex >= Inventory.Length - 1) InvIndex = 0;
            else InvIndex++;
        }
        if (_inputs.ScrollWheel > 0f)
        {
            if (InvIndex <= 0) InvIndex = Inventory.Length - 1;
            else InvIndex--;
        }
        if (_inputs.NumKey >= 0 && _inputs.NumKey < Inventory.Length)
        {
            InvIndex = _inputs.NumKey;
        }

        if(prevIndex != InvIndex && Inventory[prevIndex] != Inventory[InvIndex])
        {
            ReadyPull = false;

            float _pullOutTime = Inventory[InvIndex].itemData.pullOutSpeed;

            StopCoroutine("WaitToReadyPull");
            StartCoroutine(WaitToReadyPull(_pullOutTime));

            Select(InvIndex, _pullOutTime);
            SelectServerRpc(InvIndex, _pullOutTime);
        } 
    }


    [ServerRpc(RequireOwnership = true)]
    public void SelectServerRpc(int _index, float _pullOutTime)
    {
        SelectClientRpc(_index, _pullOutTime);
    }

    [ClientRpc]
    public void SelectClientRpc(int _index, float _pullOutTime)
    {
        if(IsOwner) return;
        Select(_index, _pullOutTime);
    }    
    
    public void Select(int _index, float _pullOutTime)
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i] != HandsData.GetComponent<Item>())
            {
                ClientInventory[i].gameObject.SetActive(i == _index);
            }
        }
        
        animations.SwitchItemAnimation(_pullOutTime*1.5f);
    }

    IEnumerator WaitToReadyPull(float _pullOutTime) {
        yield return new WaitForSeconds(_pullOutTime);
        ReadyPull = true;
    }
        
}
