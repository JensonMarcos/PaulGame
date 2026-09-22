using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections;
using System.Linq;

public enum GameState
{
    Lobby,
    MoveRoom,
    GameStart,
    InGame,
    GameEnd,
    GameOver
}

[System.Serializable]
public struct TeamInfo
{
    public string name;
    public Color color;
}

[System.Serializable]
public class Rooms
{
    public Gamemode previous, current, next;

    public void AddRoom(Gamemode room)
    {
        if(current == null)
            current = room;
        else
            next = room;
    }

    public void NextRoom()
    {
        if(previous != null) previous.GetComponent<NetworkObject>().Despawn(true);

        previous = current;
        current = next;
        next = null;
    }
}

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;
    PlayerManager playerManager;

    public NetworkVariable<FixedString128Bytes> GameTitle = new NetworkVariable<FixedString128Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    GameState _gameState;
    public GameState GameState
    {
        get => _gameState;
        set
        {
            if (_gameState == value) return;
            _gameState = value;
            OnGameStateChange(_gameState);
        }
    }
    

    [Space]
    public List<GameObject> worldObjects;
    public ItemList itemList;

    [Space]
    [Header("Rooms")]
    [SerializeField] GameObject startingRoom;
    public Rooms rooms;

    [Space]
    [Header("GameMode")]
    [SerializeField] GameObject[] roomPrefabs;
    [SerializeField] float unpickedWeightBonus;

    float[] baseWeight;
    float[] realWeight;
    GameObject lastRoomPrefab;
    bool hasLastRoom;

    [Space]
    [Header("Teams")]
    public Color defaultTeamColor = new Color(1f, 0.5f, 0f); //no team FFA
    public TeamInfo[] teams = new TeamInfo[8]; 

    [Space]
    [Header("Timers")]
    [SerializeField] float moveTime; 
    [SerializeField] float startGameTime;
    [SerializeField] float endGameTime;
    float timer;
    float doorCloseKillTime;
    bool pendingDoorCloseKill;

    void Awake()
    {
        instance = this; //have to put this here bc dumb update order
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GameState = GameState.Lobby;
        rooms.AddRoom(startingRoom.GetComponent<Gamemode>());
        SetTitle("Waiting to start");
    }

    void LobbyStart() //wtf
    {
        if (PlayerManager.instance == null) return;

        playerManager = PlayerManager.instance;
        playerManager.damageEnabled.Value = false;
        playerManager.reloadEnabled.Value = false;

        InitWeights();
        CreateRoom();
    }

    void OnGameStateChange(GameState newState)
    {
        switch (newState)
        {
            case GameState.Lobby:
                break;
            case GameState.MoveRoom:
                playerManager.damageEnabled.Value = false;
                playerManager.RespawnEveryone();

                for (int i = 0; i < playerManager.Players.Count; i++) //move people behind
                {
                    if(rooms.current.moveSpawnPoint == null) continue;
                    if (Vector3.Distance(playerManager.Players[i].player.playerCharacter.Motor.transform.position, rooms.current.moveSpawnPoint.position) > 10f && playerManager.Players[i].player.playerCharacter.Motor.transform.position.z < rooms.current.moveSpawnPoint.position.z + 5f)
                        playerManager.Teleport(playerManager.Players[i].ClientId, rooms.current.moveSpawnPoint.position);
                }

                rooms.NextRoom();

                rooms.current.Prepare();

                timer = Time.time + moveTime;

                SetTitle("Move");
                
                CreateRoom(); //create next room
                break;
            case GameState.GameStart:
                if (rooms.current.DamageEnabled) playerManager.damageEnabled.Value = true;

                rooms.current.DoorClientRpc(doorState.enter);
                rooms.previous.DoorClientRpc(doorState.closed);

                SetTitle(rooms.current.displayName);

                timer = Time.time + startGameTime;
                break;
            case GameState.InGame:
                rooms.current.DoorClientRpc(doorState.closed);

                pendingDoorCloseKill = true;
                doorCloseKillTime = Time.time + GetDoorCloseDuration(rooms.current);

                for (int i = 0; i < playerManager.Players.Count; i++)
                {
                    playerManager.Players[i].score = 0;

                    //kill players in prev room (not corridor though)
                    if (rooms.previous.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                    {
                        playerManager.WorldDamage(playerManager.Players[i].ClientId, 1000f, Vector3.zero);
                        GameTeleport(playerManager.Players[i].ClientId);
                    }
                }

                playerManager.AssignTeamsFFA();
                rooms.current.Begin();
                break;
            case GameState.GameEnd:
                rooms.current.End();

                playerManager.AssignTeamsFFA(); //back to no team, so everyone goes back to the default colour

                CleanObjects();
            
                playerManager.damageEnabled.Value = false;
                for (int i = 0; i < playerManager.Players.Count; i++)
                    playerManager.ClearItem(playerManager.Players[i].ClientId);
                playerManager.UpdateCrowns(false);

                rooms.current.DoorClientRpc(doorState.exit); 

                timer = Time.time + endGameTime;

                break;
            case GameState.GameOver:
                break;
        }
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        if(playerManager == null) LobbyStart();

        #if UNITY_EDITOR
            if (Keyboard.current.lKey.wasPressedThisFrame) GameState = GameState.GameEnd;
        #endif

        switch (GameState)
        {
            case GameState.Lobby:
                break;
            case GameState.MoveRoom:
                if (Time.time >= timer || rooms.previous.playersInRoom.Count == 0)
                {
                    GameState = GameState.GameStart;
                }

                break;
            case GameState.GameStart:
                if (Time.time >= timer)
                {
                    GameState = GameState.InGame;
                }

                break;
            case GameState.InGame:
                if (pendingDoorCloseKill)
                {
                    if (Time.time < doorCloseKillTime) break;

                    pendingDoorCloseKill = false;
                    for (int i = 0; i < playerManager.Players.Count; i++)
                    {
                        if (!rooms.current.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                        {
                            playerManager.WorldDamage(playerManager.Players[i].ClientId, 1000f, Vector3.zero);
                            GameTeleport(playerManager.Players[i].ClientId);
                        }
                    }

                    if (playerManager.playersAlive == 0)
                    {
                        SetTitle("Bruh");
                        GameState = GameState.GameEnd;
                        break;
                    }
                }

                rooms.current.Tick();
                break;
            case GameState.GameEnd:
                if (Time.time >= timer)
                {
                    GameState = GameState.MoveRoom;
                }

                break;
            case GameState.GameOver:
                // Handle game over state
                break;
        }
    }


    public void EndRound()
    {
        if (GameState != GameState.InGame) return;
        GameState = GameState.GameEnd;
    }

    public void SetTitle(string text)
    {
        if (text == null) text = "";
        if (GameTitle.Value.ToString() == text) return;
        GameTitle.Value = text;
    }

    public void AwardWin(PlayerData winner)
    {
        winner.wins++;
        playerManager.UpdatePlayerScoreboard(winner.ClientId);
        SetTitle(winner.name + " won");
    }

    public void DeclareWinners(List<PlayerData> players, bool allowMultipleWinners)
    {
        if (players.Count == 0)
        {
            SetTitle("Nobody won");
            return;
        }

        int topScore = players.Max(p => p.score);
        if (topScore <= 0)
        {
            SetTitle("Nobody won");
            return;
        }

        List<PlayerData> winners = allowMultipleWinners
            ? players.Where(p => p.score == topScore).ToList()
            : new List<PlayerData> { players.First(p => p.score == topScore) };

        foreach (PlayerData winner in winners)
        {
            winner.wins++;
            playerManager.UpdatePlayerScoreboard(winner.ClientId);
        }

        //GameTitle is a FixedString128Bytes so only fit as many names as we can, rest becomes "..."
        string title = "";
        for (int i = 0; i < winners.Count; i++)
        {
            string next = (title.Length == 0 ? "" : ", ") + winners[i].name;
            if (System.Text.Encoding.UTF8.GetByteCount(title + next + "... won") > 127)
            {
                title += "...";
                break;
            }
            title += next;
        }

        SetTitle(title + " won");
    }

    public void GameTeleport(ulong playerId)
    {
        int playerIndex = playerManager.Players.FindIndex(x => x.ClientId == playerId);
        if(playerIndex < 0) return;

        Vector3 pos;
        switch (GameState)
        {
            case GameState.MoveRoom:
                pos = rooms.previous.moveSpawnPoint.position;
                break;
            case GameState.GameEnd:
                pos = rooms.current.moveSpawnPoint.position;
                break;
            case GameState.GameStart:
                
                if (!rooms.current.playersInRoom.Contains(playerManager.Players[playerIndex].playerGameObject))
                    pos = rooms.previous.moveSpawnPoint.position; //idk prolly
                else
                    pos = rooms.current.respawnPoint.position;
                
                break;
            case GameState.InGame:
            default:
                pos = rooms.current.respawnPoint.position;
                break;
        }
        PlayerManager.instance.Teleport(playerId, pos);
    }

    public void SpawnItem(GameObject prefab, Vector3 pos)
    {
        GameObject item = Instantiate(prefab, pos, Quaternion.identity);
        item.GetComponent<NetworkObject>().Spawn(true);
        worldObjects.Add(item);
    }

    public NetworkObject SpawnItem(int itemId)
    {
        GameObject item = Instantiate(itemList.GetItem(itemId), Vector3.down, Quaternion.identity);
        NetworkObject netObj = item.GetComponent<NetworkObject>();
        netObj.Spawn(true);
        worldObjects.Add(item);
        return netObj;
    }

    void CleanObjects()
    {
        for (int i = worldObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = worldObjects[i];
            NetworkProp prop = obj.GetComponent<NetworkProp>();
            if (obj.GetComponent<Item>() != null || (prop != null && prop.rb.transform.position.y < -10) || (obj.GetComponent<ItemCrate>() != null && obj.transform.position.z < rooms.previous.transform.position.z - 10f))
            {
                obj.GetComponent<NetworkObject>().Despawn(true);
                worldObjects.RemoveAt(i);
            }
        }
    }

    public void RemoveObject(GameObject obj)
    {
        if (!worldObjects.Contains(obj)) return;
        worldObjects.Remove(obj);
        obj.GetComponent<NetworkObject>().Despawn(true);
    }

    void InitWeights()
    {
        baseWeight = new float[roomPrefabs.Length];
        realWeight = new float[roomPrefabs.Length];
        for (int i = 0; i < roomPrefabs.Length; i++)
        {
            Gamemode mode = roomPrefabs[i] != null ? roomPrefabs[i].GetComponent<Gamemode>() : null;
            baseWeight[i] = mode != null ? mode.weight : 0f;
            realWeight[i] = baseWeight[i];
        }
    }

    void CreateRoom()
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0 || rooms.current == null) return;
        if (realWeight == null) InitWeights();

        int chosen = PickRoomIndex();
        if (chosen < 0) return;

        ApplyWeightBonus(chosen);

        GameObject roomPrefab = roomPrefabs[chosen];
        GameObject newRoom = Instantiate(roomPrefab, rooms.current.nextRoomPoint.position, rooms.current.nextRoomPoint.rotation);
        newRoom.GetComponent<NetworkObject>().Spawn(true);
        rooms.AddRoom(newRoom.GetComponent<Gamemode>());

        lastRoomPrefab = roomPrefab;
        hasLastRoom = true;
    }

    int PickRoomIndex()
    {
        int chosen = WeightedIndex();
        if (chosen < 0) return -1;
        if (!hasLastRoom || roomPrefabs[chosen] != lastRoomPrefab)
            return chosen;

        bool otherHasWeight = false;
        for (int i = 0; i < realWeight.Length; i++)
        {
            if (i != chosen && realWeight[i] > 0f)
                otherHasWeight = true;
        }
        if (!otherHasWeight) return chosen;

        int guard = 0;
        while (roomPrefabs[chosen] == lastRoomPrefab && guard < 32)
        {
            int next = WeightedIndex();
            if (next < 0) break;
            chosen = next;
            guard++;
        }
        return chosen;
    }

    int WeightedIndex()
    {
        float total = 0f;
        for (int i = 0; i < realWeight.Length; i++)
            total += realWeight[i];
        if (total <= 0f) return -1;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < realWeight.Length; i++)
        {
            cumulative += realWeight[i];
            if (roll <= cumulative)
                return i;
        }
        return realWeight.Length - 1;
    }

    void ApplyWeightBonus(int chosen)
    {
        for (int i = 0; i < roomPrefabs.Length; i++)
        {
            if (baseWeight[i] == 0f) continue;
            if (i == chosen)
                realWeight[i] = Mathf.Max(0f, baseWeight[i] - unpickedWeightBonus);
            else
                realWeight[i] += unpickedWeightBonus;
        }
    }

    float GetDoorCloseDuration(Gamemode room)
    {
        if (room.doorEnter != null && room.doorEnter.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in room.doorEnter.runtimeAnimatorController.animationClips)
            {
                if (clip.name == "DoorClose") return clip.length;
            }
        }
        return 1f;
    }

    public Color GetTeamColor(int team) => (team >= 0 && team < teams.Length) ? teams[team].color : defaultTeamColor;
    public string GetTeamName(int team) => (team >= 0 && team < teams.Length) ? teams[team].name : "";
}
