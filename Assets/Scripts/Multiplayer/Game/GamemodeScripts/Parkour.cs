using UnityEngine;

public class Parkour : Gamemode
{
    const float RoundDuration = 90f;

    float endTime;

    public override bool DamageEnabled => true;
    public override bool RespawnOnDeath => true;

    public override void Begin()
    {
        endTime = Time.time + RoundDuration;
        SetTitle("");
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

        for (int i = 0; i < playerManager.Players.Count; i++)
        {
            if (playerManager.Players[i].score <= 0) continue;
            gameManager.AwardWin(playerManager.Players[i]);
            EndRound();
            return;
        }

        if (left <= 10f)
            SetTitle(((int)left).ToString());
    }
}
