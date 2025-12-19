using UnityEngine;

public class HandTarget : MonoBehaviour
{
    [SerializeField] Shooting player;
    [SerializeField] bool rightHanded, ghostItem;
    Transform target;

    void Start()
    {
        if (!player.IsOwner) Destroy(this);
    }

    void Update() {
        if(player.item == null) return;
        
        if (ghostItem) target = rightHanded ? player.clientItem.rhand : player.clientItem.lhand;
        else target = rightHanded ? player.item.rhand : player.item.lhand;
            
        transform.position = target.position;
        transform.rotation = target.rotation;
        
    }

}
