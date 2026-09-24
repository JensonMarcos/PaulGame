using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using Netcode.Transports.Facepunch;


public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance;

    const string MenuScene = "MainMenu";
    const string GameScene = "Level1";
    const int MaxPlayers = 100;

	[NonSerialized] public Lobby? CurrentLobby;
    [NonSerialized] public List<Friend> Players = new List<Friend>();

    NetworkManager networkManager;
    SteamId hostSteamId;
    bool busy;
    bool returningToMenu;
    bool quitting;
    string pendingStatus;
    ulong pendingJoinLobbyId;

    static bool IsOnline => SteamClient.IsValid && SteamClient.IsLoggedOn;
    static bool InMenu => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MenuScene;

	void Awake()
	{
		if (Instance != null)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 240;

        NetworkManager.OnInstantiated += OnNetworkManagerInstantiated;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        SteamFriends.OnGameRichPresenceJoinRequested += OnGameRichPresenceJoinRequested;
        SteamUser.OnSteamServersDisconnected += OnSteamServersDisconnected;
        SteamUser.OnSteamServersConnected += OnSteamServersConnected;
	}

    void Start()
    {
        BindNetworkManager();
        if (!SteamClient.IsValid) pendingStatus = "Steam is not running";
        OpenTitle();

        // Game launched by accepting an invite while it wasn't running
        if (TryParseConnectLobby(Environment.GetCommandLineArgs(), out ulong lobbyId)) RequestJoin(lobbyId);
    }

    void OnDestroy() {
        if (Instance != this) return;
        Instance = null;

        NetworkManager.OnInstantiated -= OnNetworkManagerInstantiated;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
        SteamFriends.OnGameRichPresenceJoinRequested -= OnGameRichPresenceJoinRequested;
        SteamUser.OnSteamServersDisconnected -= OnSteamServersDisconnected;
        SteamUser.OnSteamServersConnected -= OnSteamServersConnected;
        if (!ReferenceEquals(networkManager, null)) networkManager.OnClientStopped -= OnClientStopped;
    }

    void OnApplicationQuit()
	{
        quitting = true;
        LeaveLobby();
	}

#region Callbacks

    // MainMenu holds its own NetworkManager, so reloading it would create a second one
    void OnNetworkManagerInstantiated(NetworkManager instantiated) {
        if (NetworkManager.Singleton != null && instantiated != NetworkManager.Singleton)
            Destroy(instantiated.gameObject);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        BindNetworkManager();
        if (scene.name != MenuScene) return;

        returningToMenu = false;
        OpenTitle();

        if (pendingJoinLobbyId != 0) {
            JoinLobby(pendingJoinLobbyId);
            pendingJoinLobbyId = 0;
        }
    }

    void OnClientStopped(bool wasHost) {
        if (quitting) return;
        if (InMenu && CurrentLobby == null) return;

        string reason = networkManager != null ? networkManager.DisconnectReason : null;
        if (string.IsNullOrEmpty(reason)) reason = wasHost ? "Server stopped" : "Lost connection to host";
        HandleConnectionLost(reason);
    }

    void OnSteamServersDisconnected() {
        if (!InMenu) return; // in-game the relay connection can survive a Steam blip, OnClientStopped handles real drops

        if (CurrentLobby != null) HandleConnectionLost("Lost connection to Steam");
        else ShowStatus("Lost connection to Steam, reconnecting...");
    }

    void OnSteamServersConnected() {
        if (InMenu && CurrentLobby == null) ShowStatus("");
    }

    void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId) {
        RequestJoin(lobby.Id.Value);
    }

    // "Join Game" from the Steam friends list, uses the "connect" rich presence set in EnterLobby
    void OnGameRichPresenceJoinRequested(Friend friend, string connect) {
        if (TryParseConnectLobby(connect?.Split(' '), out ulong lobbyId)) RequestJoin(lobbyId);
    }

    void OnLobbyMemberJoined(Lobby lobby, Friend friend) {
        if (IsCurrentLobby(lobby)) UpdatePlayers();
    }

    void OnLobbyMemberLeave(Lobby lobby, Friend friend) {
        if (!IsCurrentLobby(lobby)) return;

        if (!IsHost && friend.Id.Value == hostSteamId.Value) {
            if (InMenu) HandleConnectionLost("Host left the lobby");
            else ReturnToMenu();
            return;
        }
        UpdatePlayers();
    }

#endregion

    public async void HostLobby() {
        if (busy || CurrentLobby != null || networkManager == null || MenuManager.instance == null) return;

        string lobbyName = MenuManager.instance.LobbyNameInput.text.Trim();
        if (string.IsNullOrEmpty(lobbyName)) return;
        if (!IsOnline) {
            ShowStatus("Not connected to Steam");
            return;
        }

        busy = true;
        ShowStatus("Creating lobby...");
        try {
            await WaitForShutdown();
            Lobby? created = await SteamMatchmaking.CreateLobbyAsync(MaxPlayers);
            if (this == null) return;
            if (!created.HasValue) {
                ShowStatus("Couldn't create lobby");
                return;
            }

            Lobby lobby = created.Value;
            lobby.SetPublic();
            lobby.SetData("name", lobbyName);
            lobby.SetJoinable(true);

            if (!networkManager.StartHost()) {
                lobby.Leave();
                ShowStatus("Couldn't start server");
                return;
            }

            hostSteamId = SteamClient.SteamId;
            EnterLobby(lobby);
        }
        catch (Exception e) {
            if (e is OperationCanceledException) return;
            Debug.LogException(e);
            ShowStatus("Couldn't create lobby");
        }
        finally {
            busy = false;
        }
    }

    public void JoinLobbyWithID() {
        if (MenuManager.instance == null) return;

        if (!ulong.TryParse(MenuManager.instance.LobbyIDInputField.text.Trim(), out ulong id)) {
            ShowStatus("Invalid lobby code");
            return;
        }
        JoinLobby(id);
    }

    async void JoinLobby(SteamId id) {
        if (busy || networkManager == null) return;
        if (CurrentLobby.HasValue && CurrentLobby.Value.Id.Value == id.Value) return;
        if (!IsOnline) {
            ShowStatus("Not connected to Steam");
            return;
        }

        busy = true;
        ShowStatus("Joining lobby...");
        try {
            if (CurrentLobby != null) LeaveLobby();
            await WaitForShutdown();

            Lobby lobby = new Lobby(id);
            RoomEnter result = await lobby.Join();
            if (this == null) return;
            if (result != RoomEnter.Success) {
                ShowStatus(result == RoomEnter.DoesntExist ? "Lobby doesn't exist" : $"Couldn't join lobby ({result})");
                return;
            }

            SteamId owner = lobby.Owner.Id;
            FacepunchTransport transport = networkManager.NetworkConfig.NetworkTransport as FacepunchTransport;
            if (!owner.IsValid || owner.Value == SteamClient.SteamId.Value || transport == null) {
                lobby.Leave();
                ShowStatus("Couldn't join lobby");
                return;
            }

            transport.targetSteamId = owner;
            if (!networkManager.StartClient()) {
                lobby.Leave();
                ShowStatus("Couldn't connect to host");
                return;
            }

            hostSteamId = owner;
            EnterLobby(lobby);
        }
        catch (Exception e) {
            if (e is OperationCanceledException) return;
            Debug.LogException(e);
            ShowStatus("Couldn't join lobby");
        }
        finally {
            busy = false;
        }
    }

    void RequestJoin(ulong lobbyId) {
        if (CurrentLobby.HasValue && CurrentLobby.Value.Id.Value == lobbyId) return;

        if (InMenu) {
            JoinLobby(lobbyId);
            return;
        }

        // Leave the current game first, OnSceneLoaded picks the join back up
        if (returningToMenu) return;
        pendingJoinLobbyId = lobbyId;
        ReturnToMenu();
    }

    public void InviteFriends() {
        if (CurrentLobby.HasValue && SteamClient.IsValid) SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
    }

    public void ReturnToMenu() {
        if (returningToMenu) return;
        returningToMenu = true;
        LeaveLobby();
        UnityEngine.SceneManagement.SceneManager.LoadScene(MenuScene);
    }

    public void LeaveLobby() {
        if (SteamClient.IsValid) SteamFriends.ClearRichPresence();
        if (CurrentLobby.HasValue && SteamClient.IsValid) CurrentLobby.Value.Leave();
        CurrentLobby = null;
        hostSteamId = default;

        if (networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress)
            networkManager.Shutdown();

        if (!quitting) UpdatePlayers();
    }

    public void CopyCode() {
        if (!CurrentLobby.HasValue) return;
        GUIUtility.systemCopyBuffer = CurrentLobby.Value.Id.ToString();
    }

    public void UpdatePlayers() {
        Players = CurrentLobby.HasValue && SteamClient.IsValid ? CurrentLobby.Value.Members.ToList() : new List<Friend>();

        if (MenuManager.instance != null)
            MenuManager.instance.UpdatePlayerList(Players.Select(p => p.Name).ToList());
    }

    public void StartGameServer() {
        if (networkManager == null || !networkManager.IsHost || CurrentLobby == null || !InMenu) return;

        var status = networkManager.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started) Debug.LogWarning($"Couldn't start game: {status}");
    }

    void EnterLobby(Lobby lobby) {
        CurrentLobby = lobby;
        SteamFriends.SetRichPresence("connect", $"+connect_lobby {lobby.Id.Value}");
        if (MenuManager.instance != null) MenuManager.instance.RoomJoin(lobby.Id.ToString(), lobby.GetData("name"), IsHost);
        ShowStatus(null);
        UpdatePlayers();
    }

    void HandleConnectionLost(string reason) {
        if (returningToMenu) return;

        Debug.LogWarning($"Connection lost: {reason}");
        LeaveLobby();
        pendingStatus = reason;

        if (InMenu) {
            OpenTitle();
        }
        else {
            returningToMenu = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(MenuScene);
        }
    }

    void OpenTitle() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (MenuManager.instance == null) return;
        MenuManager.instance.OpenMenu("Title");
        MenuManager.instance.ShowStatus(pendingStatus);
        pendingStatus = null;
    }

    void BindNetworkManager() {
        NetworkManager current = NetworkManager.Singleton;
        if (ReferenceEquals(current, networkManager)) return;

        if (!ReferenceEquals(networkManager, null)) networkManager.OnClientStopped -= OnClientStopped;
        networkManager = current;
        if (networkManager != null) networkManager.OnClientStopped += OnClientStopped;
    }

    async Awaitable WaitForShutdown() {
        while (networkManager != null && networkManager.ShutdownInProgress)
            await Awaitable.NextFrameAsync(destroyCancellationToken);
    }

    static bool TryParseConnectLobby(string[] args, out ulong lobbyId) {
        lobbyId = 0;
        if (args == null) return false;

        int i = Array.IndexOf(args, "+connect_lobby");
        return i >= 0 && i + 1 < args.Length && ulong.TryParse(args[i + 1], out lobbyId) && lobbyId != 0;
    }

    bool IsHost => networkManager != null && networkManager.IsHost;

    bool IsCurrentLobby(Lobby lobby) => CurrentLobby.HasValue && CurrentLobby.Value.Id.Value == lobby.Id.Value;

    void ShowStatus(string message) {
        if (MenuManager.instance != null) MenuManager.instance.ShowStatus(message);
    }
}
