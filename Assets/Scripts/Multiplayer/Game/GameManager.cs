using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] TitleSync title;

    [Space]
    public GameState gameState;
    GameState previousGameState;
    bool onGameStateChange = false;

    [Space]
    public List<GameObject> worldObjects;
    [SerializeField] GameObject[] itemPrefabs;

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


    public override void OnNetworkSpawn()
    {
        instance = this;

        if (!IsServer) return;

        gameState = GameState.Lobby;
        previousGameState = gameState;
        playerManager = PlayerManager.instance;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        #if UNITY_EDITOR
        if (Keyboard.current.lKey.wasPressedThisFrame)
        { //for testing
            gameState = GameState.GameEnd;
            CleanObjects();
        }
        #endif

        if (gameState != previousGameState)
        {
            onGameStateChange = true;
            CleanObjects();
        }
        previousGameState = gameState;

        switch (gameState)
        {
            case GameState.Lobby:
                if (roomList.Count == 0)
                {
                    roomList.Add(startingRoom);
                    currentRoom = startingRoom.GetComponent<Room>();

                    CreateRoom();
                    title.title.Value = "Lobby";
                    playerManager.damageEnabled.Value = false;
                }
                break;
            case GameState.MoveRoom:
                if (onGameStateChange) {
                    playerManager.damageEnabled.Value = false;

                    RespawnEveryone();

                    currentRoom.DoorClientRpc(1f, 1f); //prev room exit

                    currentRoom = roomList[1].GetComponent<Room>();
                    currentRoom.DoorClientRpc(0f, 1f); //new room enter

                    moveTimer = Time.time + moveTime;

                    currentGameMode = currentRoom.GetComponent<Room>().GameMode;
                    prevGameMode = currentGameMode;

                    //InitializeRoom();

                    title.title.Value = "Move";
                    
                    CreateRoom(); //create next room
                }

                if (Time.time >= moveTimer)
                {
                    gameState = GameState.GameStart;
                }

                break;
            case GameState.GameStart:
                if(onGameStateChange)
                {
                    playerManager.damageEnabled.Value = false;

                    if (prevRoom != null) prevRoom.GetComponent<NetworkObject>().Despawn(true);

                    prevRoom = roomList[0];
                    roomList.RemoveAt(0);
                    prevRoom.GetComponent<Room>().DoorClientRpc(0.5f, 1f); //close previous

                    currentRoom.DoorClientRpc(0.5f, 2f); //close current

                    title.title.Value = currentGameMode.name.ToString().Replace("_", " ");

                    startGameTimer = Time.time + startGameTime;
                }

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
                    gameState = GameState.InGame;
                }

                break;
            case GameState.InGame:
                if(onGameStateChange)
                {
                    StartRoom();
                }

                gameTimer -= Time.fixedDeltaTime;
                int _time = (int)gameTimer;
                
                if(currentGameMode.showTimer) {
                    if(title.title.Value != _time.ToString()) title.title.Value = ((int)gameTimer).ToString();
                } else title.title.Value = "";
                
                // switch (currentGameMode.name)
                // {
                //     case GameModeName.Deathmatch:
                //         Deathmatch();
                //         break;
                //     case GameModeName.King_of_the_Paul_House:
                //         King_of_the_Paul_House();
                //         break;
                //     case GameModeName.Capture_the_GPU:
                //         Capture_the_GPU();
                //         break;
                //     case GameModeName.Dont_Hold_the_C4:
                //         Dont_Hold_the_C4();
                //         break;
                //     case GameModeName.Sumo:
                //         Sumo();
                //         break;
                // }

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

                        title.title.Value = winner.ClientId.ToString() + " won";

                    } else
                    {
                        title.title.Value = "Nobody won";
                    }
                    
                    gameState = GameState.GameEnd;
                }

                if(currentGameMode.lastPlayerAliveWins && playerManager.playersAlive == 1)
                {
                    PlayerData winner = null;
                    foreach (PlayerData player in playerManager.Players)
                    {
                        if (!player.isDead) winner = player;
                    }
                    playerManager.Players[playerManager.Players.FindIndex(x => x == winner)].wins++;

                    title.title.Value = winner.ClientId.ToString() + " won";

                    gameState = GameState.GameEnd;
                }

                break;
            case GameState.GameEnd:
                if(onGameStateChange)
                {
                    playerManager.damageEnabled.Value = false;
                    currentRoom.DoorClientRpc(1f, 1f); //open current
                } 

                endGameTimer += Time.fixedDeltaTime;

                if (endGameTimer >= endGameTime)
                {
                    endGameTimer = 0f;
                    gameState = GameState.MoveRoom;
                }

                break;
            case GameState.GameOver:
                // Handle game over state
                break;
        }
    
        onGameStateChange = false;
    }

    void CreateRoom()
    {
        GameMode roomGameMode = prevGameMode;

        float totalweight = 0;
        foreach(var mode in gameModes)
        {
            totalweight += mode.gameModeWeight;
        }
        
        float rand = Random.Range(0f, totalweight);
        float currentWeight = 0f;
        foreach(var mode in gameModes)
        {
            currentWeight += mode.gameModeWeight;
            if(rand <= currentWeight)
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

    void Deathmatch()
    {
        if(playerManager.damageEnabled.Value == false) playerManager.damageEnabled.Value = true;

        if (playerManager.playersAlive == 1)
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

            title.title.Value = winner.ClientId.ToString() + " won";

            gameState = GameState.GameEnd;
        }
    }

    void King_of_the_Paul_House()
    {
        // Handle King of the Paul House game mode
    }

    void Capture_the_GPU()
    {
        // Handle Hold the GPU game mode
    }
    void Dont_Hold_the_C4()
    {
        // Handle Don't Hold the C4 game mode

    }
    void Sumo()
    {
        // Handle Sumo game mode
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
            GameObject item = Instantiate(itemPrefabs[Random.Range(0, itemPrefabs.Length)], pos, Quaternion.identity);
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

    // void CreateGameModeMap(GameObject GMPrefab) {
    //     if (GMPrefab == null) return;
    //     GameObject gameModeObject = Instantiate(GMPrefab, currentRoom.GetComponent<Room>().objectivePoint.position, Quaternion.identity);
    //     gameModeObject.GetComponent<NetworkObject>().Spawn(true);
    //     worldObjects.Add(gameModeObject);
    // }
}
