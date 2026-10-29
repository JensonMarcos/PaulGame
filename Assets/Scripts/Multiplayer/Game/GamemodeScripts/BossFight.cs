using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BossFight : Gamemode
{
    [SerializeField] GameObject bossPrefab;
    [SerializeField] Transform bossSpawn;
    [SerializeField] GameObject itemCratePrefab;
    [SerializeField] Transform itemCrateSpawns;
    [SerializeField] int bossLossPenalty = 5;

    const float RoundDuration = 120f;
    const int BossTeam = 0;
    const int HunterTeam = 1;

    readonly List<ItemCrate> crates = new List<ItemCrate>();
    PlayerData boss;
    float endTime;

    public override bool DamageEnabled => true;
    public override int ScoreOnKill => 100;

    public override void Prepare()
    {
        base.Prepare();
        if (!IsServer) return;

        SpawnCrates();

        boss = PickBoss();
        if (boss == null) return;

        playerManager.SpawnBoss(boss.ClientId, bossPrefab);
        if (bossSpawn != null)
            playerManager.Teleport(boss.ClientId, bossSpawn.position);
    }

    public override void Begin()
    {
        endTime = Time.time + RoundDuration;
        SetTitle("");
        AssignTeams();
    }

    public override void Tick()
    {
        if (boss == null || !playerManager.Players.Contains(boss) || boss.isDead)
        {
            HuntersWin();
            return;
        }

        if (!AnyHunterAlive())
        {
            gameManager.AwardWin(boss);
            EndRound();
            return;
        }

        float left = endTime - Time.time;
        if (left <= 0f)
        {
            HuntersWin();
            return;
        }

        if (left <= 10f)
            SetTitle(((int)left).ToString());
    }

    public override void End()
    {
        for (int i = 0; i < crates.Count; i++)
        {
            if (crates[i] != null)
                crates[i].lootEnabled = false;
        }

        if (boss != null)
            playerManager.DespawnBoss(boss.ClientId);
        boss = null;
    }

    PlayerData PickBoss()
    {
        List<PlayerData> candidates = new List<PlayerData>();
        int mostWins = int.MinValue;

        foreach (PlayerData player in playerManager.Players)
        {
            if (player.wins > mostWins)
            {
                mostWins = player.wins;
                candidates.Clear();
            }
            if (player.wins == mostWins)
                candidates.Add(player);
        }

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    void SpawnCrates()
    {
        if (itemCratePrefab == null || itemCrateSpawns == null) return;

        foreach (Transform spawnPoint in itemCrateSpawns)
        {
            GameObject obj = Instantiate(itemCratePrefab, spawnPoint.position, spawnPoint.rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            ItemCrate crate = obj.GetComponent<ItemCrate>();
            if (crate != null)
            {
                crate.lootEnabled = true;
                crates.Add(crate);
            }
            gameManager.worldObjects.Add(obj);
        }
    }

    void AssignTeams()
    {
        foreach (PlayerData player in playerManager.Players)
        {
            player.team = player == boss ? BossTeam : HunterTeam;
            if (player.player != null) player.player.Team.Value = player.team;
        }
    }

    bool AnyHunterAlive()
    {
        foreach (PlayerData player in playerManager.Players)
        {
            if (player != boss && !player.isDead) return true;
        }
        return false;
    }

    void HuntersWin()
    {
        if (boss != null && playerManager.Players.Contains(boss))
        {
            boss.wins = Mathf.Max(0, boss.wins - bossLossPenalty);
            playerManager.UpdatePlayerScoreboard(boss.ClientId);
        }

        SetTitle(boss != null ? boss.name + " was defeated" : "bruh");
        EndRound();
    }
}
