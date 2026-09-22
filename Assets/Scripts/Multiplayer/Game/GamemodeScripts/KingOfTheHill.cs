using UnityEngine;

public class KingOfTheHill : Gamemode
{
    [SerializeField] int itemId = 100;

    const float RoundDuration = 30f;

    float endTime;

    public override void Begin()
    {
        endTime = Time.time + RoundDuration;

        for (int i = 0; i < playerManager.Players.Count; i++)
            playerManager.GiveItem(itemId, playerManager.Players[i].ClientId);

        ShowTime();
    }

    public override void Tick()
    {
        playerManager.UpdateCrowns(true);

        if (endTime - Time.time <= 0f)
        {
            gameManager.DeclareWinners(playerManager.Players, false);
            EndRound();
            return;
        }

        ShowTime();
    }

    void ShowTime()
    {
        int seconds = Mathf.Max(0, (int)(endTime - Time.time));
        SetTitle(seconds.ToString());
    }
}
