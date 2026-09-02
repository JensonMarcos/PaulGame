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
    public int scoreOnKill;

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

    [Space]
    [Header("Script")]
    public GameObject gamemodeScript; //prefab with a GamemodeScript on its root, instantiated when the gamemode starts
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

    GamemodeScript activeGamemodeScript;

    [Space]
    [Header("Timers")]
    [SerializeField] float moveTime; 
    [SerializeField] float startGameTime;
    [SerializeField] float endGameTime;
    float timer;
    float doorCloseKillTime;
    bool pendingDoorCloseKill;
    int previousDisplayTime;

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

                for (int i = 0; i < playerManager.Players.Count; i++) //move people behind
                {
                    if(rooms.current.moveSpawnPoint == null) continue;
                    if (playerManager.Players[i].player.playerCharacter.Motor.transform.position.z < rooms.current.moveSpawnPoint.position.z - 10f)
                        playerManager.Teleport(playerManager.Players[i].ClientId, rooms.current.moveSpawnPoint.position);
                }

                rooms.NextRoom();

                rooms.current.Initialize();

                timer = Time.time + moveTime;

                currentGameMode = rooms.current.GameMode;

                GameTitle.Value = "Move";
                
                CreateRoom(); //create next room
                break;
            case GameState.GameStart:
                //playerManager.damageEnabled.Value = false;
                if(currentGameMode.doDamage) playerManager.damageEnabled.Value = true;

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
                {
                    playerManager.Players[i].score = 0;

                    //kill players in prev room (not corridor though)
                    if (rooms.previous.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                    {
                        playerManager.WorldDamage(playerManager.Players[i].ClientId, 1000f, Vector3.zero);
                        GameTeleport(playerManager.Players[i].ClientId);
                    }
                }

                StartRoom();
                break;
            case GameState.GameEnd:
                EndGamemodeScript();

                CleanObjects();
            
                playerManager.damageEnabled.Value = false;
                for (int i = 0; i < playerManager.Players.Count; i++)
                    playerManager.ClearItem(playerManager.Players[i].ClientId);
                if(currentGameMode.showCrowns) playerManager.UpdateCrowns(false);

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
                if (activeGamemodeScript != null) activeGamemodeScript.OnGameModeFixedUpdate();

                //kill players in corridor or if in previous room somehow bet
                if (pendingDoorCloseKill && Time.time >= doorCloseKillTime)
                {
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
                        GameTitle.Value = "Bruh";
                        GameState = GameState.GameEnd;
                        break;
                    }
                }

                int displayTime = (int)(timer - Time.time);
                if(currentGameMode.showTimer || displayTime <= 10) 
                {
                    if(previousDisplayTime != displayTime)
                    {
                        previousDisplayTime = displayTime;
                        GameTitle.Value = displayTime.ToString();
                    }
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
                        playerManager.UpdatePlayerScoreboard(winner.ClientId);
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
                        playerManager.UpdatePlayerScoreboard(winner.ClientId);
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
            if(mode.gameModeWeight == 0f) continue;
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

    void StartGamemodeScript()
    {
        EndGamemodeScript(); //safety, should already be null

        if (currentGameMode.gamemodeScript == null) return;

        GamemodeScript script = currentGameMode.gamemodeScript.GetComponent<GamemodeScript>();

        activeGamemodeScript = Instantiate(script);
        activeGamemodeScript.OnGameModeStart();
    }

    void EndGamemodeScript()
    {
        if (activeGamemodeScript == null) return;

        activeGamemodeScript.OnGameModeEnd();
        Destroy(activeGamemodeScript.gameObject);
        activeGamemodeScript = null;
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
        if (currentGameMode.useTeams)
            playerManager.AssignTeamsRandomly(currentGameMode.numberOfTeams);
        else
            playerManager.AssignTeamsFFA();

        //if(currentGameMode.doDamage) playerManager.damageEnabled.Value = true;

        GameTitle.Value = "";
        timer = Time.time + currentGameMode.gameTime;

        StartGamemodeScript();

        //items
        if(currentGameMode.spawnInitialItem)
        {
            int _count = (currentGameMode.numberOfInitialItems < playerManager.Players.Count) ? currentGameMode.numberOfInitialItems : playerManager.Players.Count;
            ulong[] _shuffledIds = playerManager.Players.Select(p => p.ClientId).OrderBy(id => System.Guid.NewGuid()).ToArray();

            for(int i = 0; i < _count; i++)
            {
                playerManager.GiveItem(currentGameMode.initialItemID, _shuffledIds[i]);
            }
        }
    }
}
