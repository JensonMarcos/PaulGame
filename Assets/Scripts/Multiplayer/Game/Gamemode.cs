using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum doorState
{
    enter,
    exit,
    closed
}

[System.Serializable]
public struct SpawnObject
{
    public GameObject prefab;
    public Transform point;
}

public class Gamemode : NetworkBehaviour
{
    public float weight;
    public string displayName;

    public Transform nextRoomPoint;
    public Transform respawnPoint;
    public Transform moveSpawnPoint;
    public Animator doorEnter;
    public Animator doorExit;

    public List<SpawnObject> objectsToSpawn;
    public List<GameObject> playersInRoom;

    protected GameManager gameManager => GameManager.instance;
    protected PlayerManager playerManager => PlayerManager.instance;

    void Awake()
    {
        if (playersInRoom == null) playersInRoom = new List<GameObject>();
        if (objectsToSpawn == null) objectsToSpawn = new List<SpawnObject>();
    }

    public virtual bool DamageEnabled => false;
    public virtual bool AllowPunching => true;
    public virtual bool RespawnOnDeath => false;
    public virtual int ScoreOnKill => 0;

    public virtual void Prepare()
    {
        if (!IsServer) return;

        foreach (SpawnObject spawnObject in objectsToSpawn)
        {
            if (spawnObject.prefab == null || spawnObject.point == null) continue;
            GameObject obj = Instantiate(spawnObject.prefab, spawnObject.point.position, spawnObject.point.rotation);
            obj.GetComponent<NetworkObject>().Spawn(true);
            gameManager.worldObjects.Add(obj);
        }
    }

    public virtual void Begin() { }

    public virtual void Tick() { }

    public virtual void End() { }

    protected void SetTitle(string text)
    {
        gameManager.SetTitle(text);
    }

    protected void EndRound()
    {
        gameManager.EndRound();
    }

    protected bool EndIfLastAlive()
    {
        if (playerManager.playersAlive > 1) return false;

        PlayerData winner = null;
        for (int i = 0; i < playerManager.Players.Count; i++)
        {
            if (!playerManager.Players[i].isDead)
                winner = playerManager.Players[i];
        }

        if (winner != null)
            gameManager.AwardWin(winner);
        else
            SetTitle("Nobody won");

        EndRound();
        return true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DoorClientRpc(doorState state)
    {
        switch (state)
        {
            case doorState.enter:
                doorEnter.Play("DoorOpen");
                break;
            case doorState.exit:
                doorExit.Play("DoorOpen");
                break;
            case doorState.closed:
                if (IsPlaying(doorEnter, "DoorOpen")) doorEnter.Play("DoorClose");
                if (IsPlaying(doorExit, "DoorOpen")) doorExit.Play("DoorClose");
                break;
        }
    }

    static bool IsPlaying(Animator animator, string clipName)
    {
        if (animator == null) return false;

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        return clips.Length > 0 && clips[0].clip != null && clips[0].clip.name == clipName;
    }

    void OnTriggerEnter(Collider other)
    {
        GameObject player = other.transform.root.gameObject;
        if (player.CompareTag("Player") && player.GetComponent<NetworkObject>())
        {
            if (!playersInRoom.Contains(player))
                playersInRoom.Add(player);
        }
    }

    void OnTriggerExit(Collider other)
    {
        GameObject player = other.transform.root.gameObject;
        if (player.CompareTag("Player") && player.GetComponent<NetworkObject>())
            RemoveFromRoom(player);
    }

    public void RemoveFromRoom(GameObject player)
    {
        if (playersInRoom.Contains(player))
            playersInRoom.Remove(player);
    }
}
