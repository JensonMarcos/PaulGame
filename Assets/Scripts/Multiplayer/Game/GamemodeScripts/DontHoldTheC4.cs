using System.Collections.Generic;
using UnityEngine;

public class DontHoldTheC4 : Gamemode
{
    [SerializeField] int c4ItemId;
    [SerializeField] float checkInterval = 10f;
    [SerializeField] string explosionSound = "explosion";
    [SerializeField] float ragdollForce = 75f;

    const float RoundDuration = 300f;

    float endTime;
    float nextCheckTime;
    int lastShownSecond = -1;

    public override void Begin()
    {
        endTime = Time.time + RoundDuration;
        nextCheckTime = Time.time + checkInterval;
        lastShownSecond = -1;
        GiveC4s();
        ShowCountdown();
    }

    public override void Tick()
    {
        if (endTime - Time.time <= 0f)
        {
            SetTitle("Nobody won");
            EndRound();
            return;
        }

        if (EndIfLastAlive()) return;

        ShowCountdown();

        if (Time.time < nextCheckTime) return;

        nextCheckTime = Time.time + checkInterval;

        for (int i = 0; i < playerManager.Players.Count; i++)
        {
            PlayerData player = playerManager.Players[i];
            if (player.isDead) continue;
            if (!playerManager.HasItem(player.ClientId, c4ItemId)) continue;

            Vector3 explosionPos = player.player.playerCharacter.transform.position;
            VFXManager.instance.PlayExplosion(explosionPos);
            SoundManager.Play(explosionSound, explosionPos);

            Vector3 ragdollForceVector = -player.player.playerCharacter.transform.forward * ragdollForce;
            playerManager.WorldDamage(player.ClientId, 9999f, ragdollForceVector);
        }

        if (EndIfLastAlive()) return;
        if (playerManager.playersAlive > 1)
            GiveC4s();
    }

    void ShowCountdown()
    {
        int secondsLeft = Mathf.CeilToInt(nextCheckTime - Time.time);
        if (secondsLeft == lastShownSecond) return;

        lastShownSecond = secondsLeft;
        SetTitle("Detonation in: " + secondsLeft.ToString());
    }

    void GiveC4s()
    {
        List<PlayerData> alive = new List<PlayerData>();
        for (int i = 0; i < playerManager.Players.Count; i++)
        {
            if (!playerManager.Players[i].isDead) alive.Add(playerManager.Players[i]);
        }

        if (alive.Count == 0) return;

        for (int i = alive.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (alive[i], alive[j]) = (alive[j], alive[i]);
        }

        int bombCount = alive.Count / 2;
        for (int i = 0; i < bombCount; i++)
            playerManager.GiveItem(c4ItemId, alive[i].ClientId);
    }
}
