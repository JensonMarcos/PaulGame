using UnityEngine;

public class Sumo : Gamemode
{
    [SerializeField] int itemId = 100;

    const float RoundDuration = 60f;

    float endTime;

    public override void Begin()
    {
        endTime = Time.time + RoundDuration;
        SetTitle("");

        for (int i = 0; i < playerManager.Players.Count; i++)
            playerManager.GiveItem(itemId, playerManager.Players[i].ClientId);
    }

    public override void Tick()
    {
        float left = endTime - Time.time;
        if (left <= 0f)
        {
            SetTitle("Nobody won");
            EndRound();
            return;
        }

        if (EndIfLastAlive()) return;

        if (left <= 10f)
            SetTitle(((int)left).ToString());
    }
}
