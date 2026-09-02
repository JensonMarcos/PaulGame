using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gamemode: every [checkInterval] seconds, anyone holding the C4 explodes.
/// A new C4 is then given to a random alive player. Last one standing wins.
///
/// Setup in the GameManager inspector for this gamemode:
/// - lastPlayerAliveWins = true (so the game actually ends at 1 player)
/// - respawnOnDeath = false
/// - assign this script's prefab to the "Script" field, set c4ItemId to the C4's item id
/// </summary>
public class DontHoldTheC4 : GamemodeScript
{
    [Header("Settings")]
    [SerializeField] int c4ItemId;
    [SerializeField] float checkInterval = 10f;
    [SerializeField] SoundData explosionSound;
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
                SoundManager.instance.PlayNetworkSound(explosionSound, explosionPos);

                Vector3 ragdollForceVector = -player.player.playerCharacter.transform.forward * ragdollForce;

                playerManager.WorldDamage(player.ClientId, 9999f, ragdollForceVector);
            }

            if (playerManager.playersAlive > 1)
                GiveC4s();
        }
    }   

    bool HasC4(PlayerData player)
    {
        PlayerInventory inventory = player.player.playerInventory;
        if (inventory == null || inventory.NetworkIDInventory == null) return false;

        for (int i = 0; i < inventory.NetworkIDInventory.Count; i++)
        {
            ulong netId = inventory.NetworkIDInventory[i];
            if (netId == 0UL) continue;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netId, out NetworkObject netObj)) continue;

            if (gameManager.itemList.GetItemId(netObj.gameObject) == c4ItemId) return true;
        }
        return false;
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