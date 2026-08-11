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
public struct GameMode
{
    public string name;
    public GameObject[] roomPrefabs;
    public float gameModeWeight;

    [System.NonSerialized] public float realWeight;

    [Space]
    [Header("GameMode Settings")]
    public bool lastPlayerAliveWins;
    public bool firstToScoreWins;    
    public bool highestScoreWins;
    public bool doDamage;
    public bool doPunching;
    public bool showCrowns;
    public bool respawnOnDeath;

    [Space]
    [Header("Timer")]
    public bool showTimer;
    public float gameTime;

    [Space]
    [Header("Teams")]
    public bool useTeams;
    public int numberOfTeams;

    [Space]
    [Header("Items")]
    public bool spawnInitialItem;
    public int initialItemID;
    public int numberOfInitialItems;
}

[System.Serializable]
public class Rooms
{
    public Room previous, current, next;

    public void AddRoom(Room room)
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

    public NetworkVariable<FixedString32Bytes> GameTitle = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
    //public Room currentRoom;

    [Space]
    [Header("GameMode")]
    public GameMode[] gameModes;
    public GameMode currentGameMode;
    [SerializeField] float unpickedWeightBonus;

    GameMode lastGameMode;
    GameObject lastRoomPrefab;
    bool hasLastRoom;

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
        rooms.AddRoom(startingRoom.GetComponent<Room>());
        GameTitle.Value = "Waiting to start";
    }

    void LobbyStart() //wtf
    {
        if (PlayerManager.instance == null) return;

        playerManager = PlayerManager.instance;
        playerManager.damageEnabled.Value = false;
        playerManager.reloadEnabled.Value = false;

        for (int i = 0; i < gameModes.Length; i++)
        {
            GameMode mode = gameModes[i];
            mode.realWeight = mode.gameModeWeight;
            gameModes[i] = mode;
        }

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

                rooms.NextRoom();

                rooms.current.Initialize();

                timer = Time.time + moveTime;

                currentGameMode = rooms.current.GameMode;

                GameTitle.Value = "Move";
                
                CreateRoom(); //create next room
                break;
            case GameState.GameStart:
                playerManager.damageEnabled.Value = false;

                rooms.current.DoorClientRpc(doorState.enter);
                rooms.previous.DoorClientRpc(doorState.closed);

                GameTitle.Value = currentGameMode.name.Replace("_", " ");

                timer = Time.time + startGameTime;
                break;
            case GameState.InGame:
                rooms.current.DoorClientRpc(doorState.closed);

                pendingDoorCloseKill = true;
                doorCloseKillTime = Time.time + GetDoorCloseDuration(rooms.current);

                for (int i = 0; i < playerManager.Players.Count; i++)
                    playerManager.Players[i].score = 0;

                StartRoom();
                break;
            case GameState.GameEnd:
                playerManager.damageEnabled.Value = false;
                playerManager.ClearItemServerRpc();
                if(currentGameMode.showCrowns) playerManager.UpdateCrowns(false);

                CleanObjects();

                rooms.current.crateLootEnabled = false;
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
                if (pendingDoorCloseKill && Time.time >= doorCloseKillTime)
                {
                    pendingDoorCloseKill = false;
                    for (int i = 0; i < playerManager.Players.Count; i++)
                    {
                        if (!rooms.current.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                        {
                            playerManager.DealDamageServerRpc(playerManager.Players[i].ClientId, 1234f, Vector3.zero, Vector3.zero);
                            GameTeleport(playerManager.Players[i].ClientId);
                        }
                    }

                    if (playerManager.playersAlive == 0)
                    {
                        GameTitle.Value = "Bruh";
                        GameState = GameState.GameEnd;
                        break;
                    }
                }

                int displayTime = (int)(timer - Time.time);
                if(currentGameMode.showTimer || displayTime <= 10) 
                {
                    if(GameTitle.Value != displayTime.ToString()) GameTitle.Value = displayTime.ToString();
                }

                if(currentGameMode.showCrowns)
                {
                    playerManager.UpdateCrowns(true);
                }

                if(Time.time >= timer)
                {
                    if(currentGameMode.highestScoreWins)
                    {
                        PlayerData winner = null;
                        foreach (PlayerData player in playerManager.Players)
                        {
                            if (winner == null)
                            {
                                winner = player;
                                continue;
                            }
                            if (player.score > winner.score) winner = player;
                        }
                        winner.wins++;
                        playerManager.UpdatePlayerScoreboardServerRpc(winner.ClientId);
                        GameTitle.Value = winner.name + " won";

                    } else
                    {
                        GameTitle.Value = "Nobody won";
                    }
                    
                    GameState = GameState.GameEnd;
                }

                if(currentGameMode.lastPlayerAliveWins && playerManager.playersAlive <= 1)
                {
                    PlayerData winner = null;
                    foreach (PlayerData player in playerManager.Players)
                    {
                        if (!player.isDead) winner = player;
                    }

                    if(winner != null)
                    {
                        winner.wins++;
                        playerManager.UpdatePlayerScoreboardServerRpc(winner.ClientId);
                        GameTitle.Value = winner.name + " won";
                    } else
                    {
                        GameTitle.Value = "Nobody won";
                    }
                
                    GameState = GameState.GameEnd;
                }

                if(currentGameMode.firstToScoreWins)
                {
                    foreach (PlayerData player in playerManager.Players)
                    {
                        if(player.score > 1f)
                        {
                            GameTitle.Value = player.name + " won";
                            GameState = GameState.GameEnd;
                            break;
                        }
                    }
                }

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


    public void GameTeleport(ulong playerId)
    {
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
                
                int i = playerManager.Players.FindIndex(x => x.ClientId == playerId);
                if (!rooms.current.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                    pos = rooms.previous.moveSpawnPoint.position; //idk prolly
                else
                    pos = rooms.current.respawnPoint.position;
                
                break;
            case GameState.InGame:
            default:
                pos = rooms.current.respawnPoint.position;
                break;
        }
        PlayerManager.instance.TeleportServerRpc(playerId, pos);
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

    void CreateRoom()
    {
        GameMode roomGameMode = new GameMode();
        int chosenIndex = 0;

        float totalweight = 0;
        for (int i = 0; i < gameModes.Length; i++)
            totalweight += gameModes[i].realWeight;
        
        float randomWeight = Random.Range(0f, totalweight);
        float cumulativeWeight = 0f;

        for (int i = 0; i < gameModes.Length; i++)
        {
            cumulativeWeight += gameModes[i].realWeight;
            if (randomWeight <= cumulativeWeight)
            {
                roomGameMode = gameModes[i];
                chosenIndex = i;
                break;
            }
        }

        //set dynamic weights
        for (int i = 0; i < gameModes.Length; i++)
        {
            GameMode mode = gameModes[i];
            if (i == chosenIndex)
                mode.realWeight = Mathf.Max(0f, mode.gameModeWeight - unpickedWeightBonus);
            else
                mode.realWeight += unpickedWeightBonus;
            gameModes[i] = mode;
        }

        GameObject roomPrefab = roomGameMode.roomPrefabs[Random.Range(0, roomGameMode.roomPrefabs.Length)];

        //if the gamemode repeats, dont pick the same map again (unless its the only one)
        if (hasLastRoom && roomGameMode.name == lastGameMode.name && roomGameMode.roomPrefabs.Length > 1)
        {
            while (roomPrefab == lastRoomPrefab)
                roomPrefab = roomGameMode.roomPrefabs[Random.Range(0, roomGameMode.roomPrefabs.Length)];
        }

        GameObject newRoom = Instantiate(roomPrefab, rooms.current.nextRoomPoint.position, rooms.current.nextRoomPoint.rotation);
        newRoom.GetComponent<NetworkObject>().Spawn(true);
        newRoom.GetComponent<Room>().GameMode = roomGameMode;
        rooms.AddRoom(newRoom.GetComponent<Room>());

        lastGameMode = roomGameMode;
        lastRoomPrefab = roomPrefab;
        hasLastRoom = true;
    }

    float GetDoorCloseDuration(Room room)
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

    void StartRoom()
    {
        // if(currentGameMode.spawnItems)//spawn items in the room
        // {
        //     float _radius = 10f;
        //     for (int i = 0; i < (int)(playerManager.Players.Count * 1.5f) + 4; i++)
        //     {
        //         Vector2 ranCircle = Random.insideUnitCircle * _radius;
        //         Vector3 pos = rooms.current.moveSpawnPoint.position + new Vector3(ranCircle.x, 0f, ranCircle.y);
        //         pos.y = 10f;
        //         SpawnItem(itemList.GetItem(itemList.GetRandomItemId()), pos);
        //     }
        // }
        
        if (currentGameMode.useTeams)
            playerManager.AssignTeamsRandomly(currentGameMode.numberOfTeams);
        else
            playerManager.AssignTeamsFFA();

        if(currentGameMode.doDamage) playerManager.damageEnabled.Value = true;

        GameTitle.Value = "";
        timer = Time.time + currentGameMode.gameTime;

        //items
        if(currentGameMode.spawnInitialItem)
        {
            int _count = (currentGameMode.numberOfInitialItems < playerManager.Players.Count) ? currentGameMode.numberOfInitialItems : playerManager.Players.Count;
            ulong[] _shuffledIds = playerManager.Players.Select(p => p.ClientId).OrderBy(id => System.Guid.NewGuid()).ToArray();

            for(int i = 0; i < _count; i++)
            {
                playerManager.GiveItemServerRpc(currentGameMode.initialItemID, _shuffledIds[i]);
            }
        }
    }
}
