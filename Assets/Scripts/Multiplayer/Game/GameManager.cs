using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Collections;
using Unity.VisualScripting;

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

    [Space]
    [Header("Timers")]
    [SerializeField] float moveTime; 
    [SerializeField] float startGameTime;
    [SerializeField] float endGameTime;
    float timer;

    void Awake()
    {
        instance = this; //have to put this here bc dumb update order
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        playerManager = PlayerManager.instance;

        GameState = GameState.Lobby;
        rooms.AddRoom(startingRoom.GetComponent<Room>());
        GameTitle.Value = "Waiting to start";
        playerManager.damageEnabled.Value = false;
        playerManager.reloadEnabled = false;
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
                playerManager.ClearItemServerRpc();
                CleanObjects();

                rooms.NextRoom();

                rooms.previous.DoorClientRpc(doorState.exit, 1f); 
                rooms.current.DoorClientRpc(doorState.enter, 1f); 

                timer = Time.time + moveTime;

                currentGameMode = rooms.current.GameMode;

                GameTitle.Value = "Move";
                
                CreateRoom(); //create next room
                break;
            case GameState.GameStart:
                playerManager.damageEnabled.Value = false;

                rooms.previous.DoorClientRpc(doorState.closed, 1f);
                rooms.current.DoorClientRpc(doorState.closed, 2f); 

                GameTitle.Value = currentGameMode.name.Replace("_", " ");

                timer = Time.time + startGameTime;
                break;
            case GameState.InGame:
                StartRoom();
                break;
            case GameState.GameEnd:
                playerManager.damageEnabled.Value = false;
                rooms.current.DoorClientRpc(doorState.exit, 1f); 

                timer = Time.time + endGameTime;

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
                break;
            case GameState.MoveRoom:
                if (Time.time >= timer)
                {
                    GameState = GameState.GameStart;
                }

                break;
            case GameState.GameStart:
                if (Time.time >= timer)
                {
                    for (int i = 0; i < playerManager.Players.Count; i++)
                    {
                        playerManager.Players[i].score = 0;
                        if (!rooms.current.playersInRoom.Contains(playerManager.Players[i].playerGameObject))
                        {
                            playerManager.DealDamageServerRpc(playerManager.Players[i].ClientId, 1234f, Vector3.zero);
                        }
                    }

                    GameState = GameState.InGame;
                }

                break;
            case GameState.InGame:
                int displayTime = (int)(timer - Time.time);
                if(currentGameMode.showTimer || displayTime <= 10) 
                {
                    if(GameTitle.Value != displayTime.ToString()) GameTitle.Value = displayTime.ToString();
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
                        GameTitle.Value = winner.name + " won";
                    } else
                    {
                        GameTitle.Value = "Nobody won";
                    }
                
                    GameState = GameState.GameEnd;
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
            if (obj.GetComponent<Item>() != null || (prop != null && prop.rb.transform.position.y < -10))
            {
                obj.GetComponent<NetworkObject>().Despawn(true);
                worldObjects.RemoveAt(i);
            }
        }
    }

    void CreateRoom()
    {
        GameMode roomGameMode = new GameMode();

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

        GameObject newRoom = Instantiate(roomGameMode.roomPrefabs[Random.Range(0, roomGameMode.roomPrefabs.Length)], rooms.current.spawnPoint.position, rooms.current.spawnPoint.rotation);
        newRoom.GetComponent<NetworkObject>().Spawn(true);
        newRoom.GetComponent<Room>().GameMode = roomGameMode;
        rooms.AddRoom(newRoom.GetComponent<Room>());
    }

    void StartRoom()
    {
        if(currentGameMode.spawnItems)//spawn items in the room
        {
            float _radius = 10f;
            for (int i = 0; i < (int)(playerManager.Players.Count * 1.5f) + 4; i++)
            {
                Vector2 ranCircle = Random.insideUnitCircle * _radius;
                Vector3 pos = rooms.current.objectivePoint.position + new Vector3(ranCircle.x, 0f, ranCircle.y);
                pos.y = 10f;
                SpawnItem(itemList.GetItem(itemList.GetRandomItemId()), pos);
            }
        }
        
        if (currentGameMode.useTeams)
            playerManager.AssignTeamsRandomly(currentGameMode.numberOfTeams);
        else
            playerManager.AssignTeamsFFA();

        if(currentGameMode.doDamage) playerManager.damageEnabled.Value = true;

        GameTitle.Value = "";
        timer = Time.time + currentGameMode.gameTime;
    }
}
