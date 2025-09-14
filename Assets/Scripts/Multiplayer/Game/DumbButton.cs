using UnityEngine;

public class DumbButton : MonoBehaviour
{
    bool pressed = false;

    void Update()
    {
        if(!GameManager.instance.IsServer) return;
        if(!pressed && transform.localPosition.z > 0.1f) {
            GameManager.instance.gameState = GameState.MoveRoom;
            print("start");
            pressed = true;
            this.enabled = false;
        }
    }
}
