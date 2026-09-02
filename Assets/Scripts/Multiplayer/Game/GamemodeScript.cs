using UnityEngine;

public abstract class GamemodeScript : MonoBehaviour
{
    protected GameManager gameManager => GameManager.instance;
    protected PlayerManager playerManager => PlayerManager.instance;
    protected GameMode gameMode => GameManager.instance.currentGameMode;

    public virtual void OnGameModeStart() { }

    public virtual void OnGameModeFixedUpdate() { }

    public virtual void OnGameModeEnd() { }
}