using System.Collections.Generic;
using UnityEngine;

public class DontHoldTheC4 : GamemodeScript
{
    [Header("Settings")]
    [SerializeField] int c4ItemId;
    [SerializeField] float checkInterval = 10f;
    [SerializeField] string explosionSound = "explosion";
    [SerializeField] float ragdollForce = 75f;

    float nextCheckTime;
    int lastShownSecond = -1;

    public override void OnGameModeStart()
    {
        nextCheckTime = Time.time + checkInterval;
        GiveC4s();
    }

    public override void OnGameModeFixedUpdate()
    {
        if (playerManager.playersAlive <= 1) return;

        //show the bomb countdown in the title (once per second)
        int secondsLeft = Mathf.CeilToInt(nextCheckTime - Time.time);
        if (secondsLeft != lastShownSecond)
        {
            lastShownSecond = secondsLeft;
            gameManager.GameTitle.Value = "Detonation in: " + secondsLeft.ToString();
        }

        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;

            for (int i = 0; i < playerManager.Players.Count; i++)
            {
                PlayerData player = playerManager.Players[i];
                if (player.isDead) continue;
                if (!HasC4(player)) continue;

                Vector3 explosionPos = player.player.playerCharacter.transform.position;
                VFXManager.instance.PlayExplosion(explosionPos);
                SoundManager.Play(explosionSound, explosionPos);

                Vector3 ragdollForceVector = -player.player.playerCharacter.transform.forward * ragdollForce;

                playerManager.WorldDamage(player.ClientId, 9999f, ragdollForceVector);
            }

            if (playerManager.playersAlive > 1)
                GiveC4s();
        }
    }   

    bool HasC4(PlayerData player)
    {
        return playerManager.HasItem(player.ClientId, c4ItemId);
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

        int bombCount = alive.Count / 2; //half the alive players, rounded down
        for (int i = 0; i < bombCount; i++)
            playerManager.GiveItem(c4ItemId, alive[i].ClientId);
    }
}