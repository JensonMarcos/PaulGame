using UnityEngine;

public class DumbButton : MonoBehaviour
{
    bool pressed = false;

    void Update()
    {
        if(GameManager.instance == null || !GameManager.instance.IsServer) return;
        if(!pressed && transform.localPosition.z > 0.1f) {
            GameManager.instance.GameState = GameState.MoveRoom;
            print("start");
            pressed = true;
            this.enabled = false;
        }
    }
}
