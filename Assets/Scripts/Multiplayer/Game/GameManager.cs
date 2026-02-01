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

public enum GameMode
{
    Deathmatch,
    King_of_the_Paul_House,
    Capture_the_GPU,
    Dont_Hold_the_C4,
    Sumo,
    Soccer_Pall
}

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    public TitleSync title;

    public GameState gameState;
    GameState previousGameState;
    [SerializeField] GameObject[] roomPrefabs;
    [SerializeField] GameObject startingRoom;
    public List<GameObject> roomList;
    public int roomIndex;
    public Room currentRoom;
    [SerializeField] float moveTime, moveTimer;
    [SerializeField] float gameTime, gameTimer;
    [SerializeField] float endGameTime, endGameTimer;

    public List<GameObject> worldObjects;

    [SerializeField] GameObject[] itemPrefabs;

    [Header("GameMode")]

    public GameMode gameMode;
    GameMode prevGamemode;
    GameObject prevRoom;
    public bool gameReady;

    [SerializeField] GameObject[] DMPrefabs, KingPrefabs, GPUPrefabs, C4Prefabs, SumoPrefabs, SoccerPrefabs;

    public override void OnNetworkSpawn()
    {
        instance = this;

        if (!IsServer) return;

        gameState = GameState.Lobby;
        previousGameState = gameState;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        #if UNITY_EDITOR
        if (Keyboard.current.lKey.wasPressedThisFrame)
        { //for testing
            gameState = GameState.MoveRoom;
            cleanObjects();
        }
        #endif

        if (gameState != previousGameState)
        {
            cleanObjects();
        }
        previousGameState = gameState;

        switch (gameState)
        {
            case GameState.Lobby:
                if (roomList.Count == 0)
                {
                    roomList.Add(startingRoom);
                    currentRoom = startingRoom.GetComponent<Room>();

                    createRoom();
                    title.title.Value = "Lobby";
                }
                break;
            case GameState.MoveRoom:
                gameReady = false;
                if (roomList.Count == 2)
                { //just changed
                    RespawnEveryone();

                    currentRoom.DoorClientRpc(1f, 1.5f); //prev room exit

                    currentRoom = roomList[1].GetComponent<Room>();
                    currentRoom.DoorClientRpc(0f, 0.75f); //new room enter

                    moveTimer = Time.time + moveTime;

                    gameMode = currentRoom.GetComponent<Room>().roomGameMode;
                    prevGamemode = gameMode;

                    InitializeRoom();

                    title.title.Value = "Move";
                    
                    createRoom(); //create next room
                }

                if (Time.time >= moveTimer)
                {
                    gameState = GameState.GameStart;
                }

                break;
            case GameState.GameStart:
                if (roomList.Count == 3) //just entered
                {
                    if (prevRoom != null)
                    { //destroy previous room on the next cycle (for animation stuff)
                        prevRoom.GetComponent<NetworkObject>().Despawn(true);
                    }

                    currentRoom.DoorClientRpc(0.5f, 0.75f); //close current
                    prevRoom = roomList[0];
                    roomList.RemoveAt(0);
                    prevRoom.GetComponent<Room>().DoorClientRpc(0.5f, 1.5f); //close previous

                    title.title.Value = gameMode.ToString().Replace("_", " ");
                }

                if (Mathf.Abs(currentRoom.anim.GetFloat("OpenState") - 0.5f) < 0.01f)
                {
                    for (int i = 0; i < PlayerManager.instance.Players.Count; i++)
                    {
                        PlayerManager.instance.Players[i].score = 0;
                        if (!currentRoom.playersInRoom.Contains(PlayerManager.instance.Players[i].playerGameObject))
                        {
                            PlayerManager.instance.DealDamageServerRpc(PlayerManager.instance.Players[i].ClientId, 1234f, Vector3.zero);
                            //PlayerManager.instance.allplayers[i].playerGameObject.transform.position = currentRoom.objectivePoint.position;
                        }
                    }

                    gameTimer = gameTime;
                    gameState = GameState.InGame;
                }

                break;
            case GameState.InGame:
                if (gameTimer <= 0 || PlayerManager.instance.playersAlive == 1)
                {
                    PlayerData winner = null;
                    foreach (PlayerData player in PlayerManager.instance.Players)
                    {
                        if (winner == null)
                        {
                            winner = player;
                            continue;
                        }
                        if (player.score > winner.score) winner = player;
                    }
                    PlayerManager.instance.Players[PlayerManager.instance.Players.FindIndex(x => x == winner)].wins++;

                    title.title.Value = winner.ClientId.ToString() + " won";

                    gameState = GameState.GameEnd;
                    break;
                }

                gameTimer -= Time.fixedDeltaTime;
                int _time = (int)gameTimer;
                if(title.title.Value != _time.ToString()) title.title.Value = ((int)gameTimer).ToString();
                
                switch (gameMode)
                {
                    case GameMode.Deathmatch:
                        Deathmatch();
                        break;
                    case GameMode.King_of_the_Paul_House:
                        King_of_the_Paul_House();
                        break;
                    case GameMode.Capture_the_GPU:
                        Capture_the_GPU();
                        break;
                    case GameMode.Dont_Hold_the_C4:
                        Dont_Hold_the_C4();
                        break;
                    case GameMode.Sumo:
                        Sumo();
                        break;
                    case GameMode.Soccer_Pall:
                        Soccer_Pall();
                        break;
                }

                break;
            case GameState.GameEnd:
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
    }

    void createRoom()
    {
        GameObject newRoom = Instantiate(roomPrefabs[Random.Range(0, roomPrefabs.Length)], currentRoom.spawnPoint.position, currentRoom.spawnPoint.rotation);
        newRoom.GetComponent<NetworkObject>().Spawn(true);
        GameMode _roomGameMode = GameMode.Deathmatch;
        //GameMode _roomGameMode = (GameMode)Random.Range(0, System.Enum.GetValues(typeof(GameMode)).Length);
        //if (_roomGameMode == prevGamemode) _roomGameMode = (GameMode)Random.Range(0, System.Enum.GetValues(typeof(GameMode)).Length);
        newRoom.GetComponent<Room>().roomGameMode = _roomGameMode;
        roomList.Add(newRoom);
    }

    void cleanObjects()
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
    void Soccer_Pall()
    {
        // Handle Soccer Pall game mode
    }

    public void RespawnEveryone()
    {
        foreach (PlayerData player in PlayerManager.instance.Players)
        {
            if (!player.isDead)
            {
                player.health = 100f;
                // player.playerGameObject.GetComponent<Health>().UpdateHealthClientRpc(player.health);
                player.score = 0;
                continue;
            }
            PlayerManager.instance.RespawnServerRpc(player.ClientId);
        }
    }

    public void SpawnItems(Vector3 center, float radius, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            Vector3 pos = center + Random.insideUnitSphere * radius;
            pos.y = 10f;
            GameObject item = Instantiate(itemPrefabs[Random.Range(0, itemPrefabs.Length)], pos, Quaternion.identity);
            item.GetComponent<NetworkObject>().Spawn(true);
            worldObjects.Add(item);
        }
    }

    void InitializeRoom()
    {
        switch (gameMode)
        {
            case GameMode.Deathmatch:
                CreateGameModeMap(DMPrefabs[Random.Range(0, DMPrefabs.Length)]);
                SpawnItems(currentRoom.objectivePoint.position, 10f, (int)(PlayerManager.instance.Players.Count * 1.5f) + 5); //spawn items in the room

                break;
            case GameMode.King_of_the_Paul_House:
                CreateGameModeMap(KingPrefabs[Random.Range(0, KingPrefabs.Length)]);
                break;
            case GameMode.Capture_the_GPU:
                CreateGameModeMap(GPUPrefabs[Random.Range(0, GPUPrefabs.Length)]);
                break;
            case GameMode.Dont_Hold_the_C4:
                CreateGameModeMap(C4Prefabs[Random.Range(0, C4Prefabs.Length)]);
                break;
            case GameMode.Sumo:
                CreateGameModeMap(SumoPrefabs[Random.Range(0, SumoPrefabs.Length)]);
                break;
            case GameMode.Soccer_Pall:
                CreateGameModeMap(SoccerPrefabs[Random.Range(0, SoccerPrefabs.Length)]);
                break;
        }
    }

    void CreateGameModeMap(GameObject GMPrefab) {
        if (GMPrefab == null) return;
        GameObject gameModeObject = Instantiate(GMPrefab, currentRoom.GetComponent<Room>().objectivePoint.position, Quaternion.identity);
        gameModeObject.GetComponent<NetworkObject>().Spawn(true);
        worldObjects.Add(gameModeObject);
    }
}
