using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Deathmatch : Gamemode
{
    [SerializeField] GameObject itemCratePrefab;
    [SerializeField] Transform itemCrateSpawns;

    const float RoundDuration = 60f;

    readonly List<ItemCrate> crates = new List<ItemCrate>();
    float endTime;

    public override bool DamageEnabled => true;
    public override int ScoreOnKill => 100;

    public override void Prepare()
    {
        base.Prepare();
        if (!IsServer || itemCratePrefab == null || itemCrateSpawns == null) return;

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

        if (EndIfLastAlive()) return;

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
    }
}
