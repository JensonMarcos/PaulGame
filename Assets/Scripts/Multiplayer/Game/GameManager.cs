using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections;

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
public enum GameModeName
{
    Deathmatch,
    King_of_the_Paul_House,
    Capture_the_GPU,
    Dont_Hold_the_C4,
    Sumo
}

[System.Serializable]
public struct GameMode
{
    public GameModeName name;
    public GameObject[] roomPrefabs;
    public float gameModeWeight;

    [Space]
    [Header("GameMode Settings")]
    public bool lastPlayerAliveWins;
    public bool spawnItems;
    public bool doDamage;

    [Space]
    [Header("Timer")]
    public bool showTimer;
    public float gameTime;
    public bool highestScoreWins;
    public float animTimeMult;

    [Space]
    [Header("Teams")]
    public bool useTeams;
    public int numberOfTeams;
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
    public List<GameObject> roomList;
    public int roomIndex;
    public Room currentRoom;

    [Space]
    [Header("GameMode")]
    public GameMode[] gameModes;
    public GameMode currentGameMode;
    GameMode prevGameMode;
    GameObject prevRoom;

    [Space]
    [Header("Timers")]
    [SerializeField] float moveTime; 
    [SerializeField] float startGameTime;
    [SerializeField] float endGameTime;
    float moveTimer, startGameTimer, gameTimer, endGameTimer;

    void Awake()
    {
        instance = this; //have to put this here bc dumb update order
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GameState = GameState.Lobby;
        playerManager = PlayerManager.instance;
        GameTitle.Value = "";
    }

    void OnGameStateChange(GameState newState)
    {
        CleanObjects();
        switch (newState)
        {
            case GameState.Lobby:
                break;
            case GameState.MoveRoom:
                playerManager.damageEnabled.Value = false;

                RespawnEveryone();

                currentRoom.DoorClientRpc(1f, 1f); //prev room exit

                currentRoom = roomList[1].GetComponent<Room>();
                currentRoom.DoorClientRpc(0f, 1f); //new room enter

                moveTimer = Time.time + moveTime;

                currentGameMode = currentRoom.GetComponent<Room>().GameMode;
                prevGameMode = currentGameMode;

                //InitializeRoom();

                GameTitle.Value = "Move";
                
                CreateRoom(); //create next room
                break;
            case GameState.GameStart:
                playerManager.damageEnabled.Value = false;

                if (prevRoom != null) prevRoom.GetComponent<NetworkObject>().Despawn(true);

                prevRoom = roomList[0];
                roomList.RemoveAt(0);
                prevRoom.GetComponent<Room>().DoorClientRpc(0.5f, 1f); //close previous

                currentRoom.DoorClientRpc(0.5f, 2f); //close current

                GameTitle.Value = currentGameMode.name.ToString().Replace("_", " ");

                startGameTimer = Time.time + startGameTime;
                break;
            case GameState.InGame:
                StartRoom();
                break;
            case GameState.GameEnd:
                playerManager.damageEnabled.Value = false;
                currentRoom.DoorClientRpc(1f, 1f); //open current
                break;
            case GameState.GameOver:
                break;
        }
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        if(playerManager == null) playerManager = PlayerManager.instance;

        #if UNITY_EDITOR
            if (Keyboard.current.lKey.wasPressedThisFrame) GameState = GameState.GameEnd;
        #endif

        switch (GameState)
        {
            case GameState.Lobby:
                if (roomList.Count == 0)
                {
                    roomList.Add(startingRoom);
                    currentRoom = startingRoom.GetComponent<Room>();

                    CreateRoom();
                    GameTitle.Value = "Lobby";
                    playerManager.damageEnabled.Value = false;
                }
                break;
            case GameState.MoveRoom:
                if (Time.time >= moveTimer)
                {
                    GameState = GameState.GameStart;
                }

                break;
            case GameState.GameStart:
                if (Time.time >= startGameTimer)
                {
                    for (int i = 0; i < playerManager.Players.Count; i++)
                    {
                        playerManager.Players[i].score = 0;
                        if (!currentRoom.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                        {
                            playerManager.DealDamageServerRpc(playerManager.Players[i].ClientId, 1234f, Vector3.zero);
                        }
                    }

                    gameTimer = currentGameMode.gameTime;
                    GameState = GameState.InGame;
                }

                break;
            case GameState.InGame:
                gameTimer -= Time.fixedDeltaTime;
                int _time = (int)gameTimer;
                
                if(currentGameMode.showTimer) {
                    if(GameTitle.Value != _time.ToString()) GameTitle.Value = ((int)gameTimer).ToString();
                } else GameTitle.Value = "";

                if(gameTimer <= 0)
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
                        playerManager.Players[playerManager.Players.FindIndex(x => x == winner)].wins++;

                        GameTitle.Value = winner.ClientId.ToString() + " won";

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
                        playerManager.Players[playerManager.Players.FindIndex(x => x == winner)].wins++;
                        GameTitle.Value = winner.ClientId.ToString() + " won";
                    } else
                    {
                        GameTitle.Value = "Nobody won";
                    }
                
                    GameState = GameState.GameEnd;
                }

                break;
            case GameState.GameEnd:
                endGameTimer += Time.fixedDeltaTime;

                if (endGameTimer >= endGameTime)
                {
                    endGameTimer = 0f;
                    GameState = GameState.MoveRoom;
                }

                break;
            case GameState.GameOver:
                // Handle game over state
                break;
        }
    }

    void CreateRoom()
    {
        GameMode roomGameMode = prevGameMode;

        float totalweight = 0;
        foreach(var mode in gameModes)
        {
            totalweight += mode.gameModeWeight;
        }
        
        float randomWeight = Random.Range(0f, totalweight);
        float cumulativeWeight = 0f;

        foreach(var mode in gameModes)
        {
            cumulativeWeight += mode.gameModeWeight;
            if(randomWeight <= cumulativeWeight)
            {
                roomGameMode = mode;
                break;
            }
        }

        GameObject newRoom = Instantiate(roomGameMode.roomPrefabs[Random.Range(0, roomGameMode.roomPrefabs.Length)], currentRoom.spawnPoint.position, currentRoom.spawnPoint.rotation);
        newRoom.GetComponent<NetworkObject>().Spawn(true);
        newRoom.GetComponent<Room>().GameMode = roomGameMode;
        roomList.Add(newRoom);
    }

    void CleanObjects()
    {   
        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i].GetComponent<NetworkProp>().rb.transform.position.y < -10)
            { //destroy objects that fall off the map
                worldObjects[i].GetComponent<NetworkObject>().Despawn(true);
                worldObjects.RemoveAt(i);
            }
        }
    }

    public void RespawnEveryone()
    {
        foreach (PlayerData player in playerManager.Players)
        {
            if (!player.isDead)
            {
                player.health = 100f;
                // player.playerGameObject.GetComponent<Health>().UpdateHealthClientRpc(player.health);
                player.score = 0;
                continue;
            }
            playerManager.RespawnServerRpc(player.ClientId);
        }
    }

    public void SpawnItems(Vector3 center, float radius, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            Vector2 ranCircle = Random.insideUnitCircle * radius;
            Vector3 pos = center + new Vector3(ranCircle.x, 0f, ranCircle.y);
            pos.y = 10f;
            GameObject item = Instantiate(itemList.GetItem(itemList.GetRandomItemId()), pos, Quaternion.identity);
            item.GetComponent<NetworkObject>().Spawn(true);
            worldObjects.Add(item);
        }
    }

    void StartRoom()
    {
        // GameObject gameModePrefab = null;
        // switch (gameMode.name)
        // {
        //     case GameModeName.Deathmatch:
        //         gameModePrefab = DMPrefabs[Random.Range(0, DMPrefabs.Length)];
        //         SpawnItems(currentRoom.objectivePoint.position, 10f, (int)(playerManager.Players.Count * 1.5f) + 5); //spawn items in the room

        //         break;
        //     case GameModeName.King_of_the_Paul_House:
        //         gameModePrefab = KingPrefabs[Random.Range(0, KingPrefabs.Length)];
        //         break;
        //     case GameModeName.Capture_the_GPU:
        //         gameModePrefab = GPUPrefabs[Random.Range(0, GPUPrefabs.Length)];
        //         break;
        //     case GameModeName.Dont_Hold_the_C4:
        //         gameModePrefab = C4Prefabs[Random.Range(0, C4Prefabs.Length)];
        //         break;
        //     case GameModeName.Sumo:
        //         gameModePrefab = SumoPrefabs[Random.Range(0, SumoPrefabs.Length)];
        //         break;
        // }
        //CreateGameModeMap(gameModePrefab);
        if(currentGameMode.spawnItems) SpawnItems(currentRoom.objectivePoint.position, 10f, (int)(playerManager.Players.Count * 1.5f) + 4); //spawn items in the room

        if(currentGameMode.doDamage) playerManager.damageEnabled.Value = true;
    }
}
