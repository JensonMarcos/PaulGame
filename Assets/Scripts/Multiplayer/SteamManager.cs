using UnityEngine;
using Steamworks;
using TMPro;
using Steamworks.Data;
using System;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using Netcode.Transports.Facepunch;
using UnityEngine.SceneManagement;
using Netcode.Transports.Facepunch;


public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance;
    //FacepunchTransport transport;

	public Lobby? CurrentLobby;
    public List<Friend> Players;

    // [SerializeField] TMP_InputField LobbyIDInputField;
    // [SerializeField] TextMeshProUGUI LobbyIDDisplay;
    // [SerializeField] TMP_InputField LobbyNameInput;
    // [SerializeField] TextMeshProUGUI LobbyName;

	void Awake()
	{
		if (Instance == null)
			Instance = this;
		else
		{
			Destroy(gameObject);
			return;
		}

		DontDestroyOnLoad(gameObject);
        Application.targetFrameRate = 240;
	}

    void Start()
    {
        StartCoroutine(Connected());
    }

    IEnumerator Connected() {
        yield return new WaitUntil(() => NetworkManager.Singleton != null);
        MenuManager.instance.OpenMenu("Title");

        //transport = NetworkManager.Singleton.GetComponent<FacepunchTransport>();

        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        // SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
    }

    void OnDisable() {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
		SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
		SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
		SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
		// SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
    }

#region Callbacks

    private void OnLobbyCreated(Result result, Lobby lobby) {
        if (result != Result.OK) {
			Debug.LogError($"Lobby couldn't be created, {result}");
			return;
		}

        lobby.SetPublic();
		//lobby.SetFriendsOnly();
		lobby.SetData("name", MenuManager.instance.LobbyNameInput.text);
		lobby.SetJoinable(true);
        NetworkManager.Singleton.StartHost();

		Debug.Log("Lobby has been created");
    }

    private void OnLobbyEntered(Lobby lobby) {
        CurrentLobby = lobby;
        if(MenuManager.instance != null) MenuManager.instance.RoomJoin(lobby.Id.ToString(), lobby.GetData("name"), NetworkManager.Singleton.IsHost);
        UpdatePlayers();

        print($"Entered a lobby, clientId={NetworkManager.Singleton.LocalClientId}");

        if(NetworkManager.Singleton.IsHost) return;
        NetworkManager.Singleton.gameObject.GetComponent<FacepunchTransport>().targetSteamId = lobby.Owner.Id;
        NetworkManager.Singleton.StartClient();
    }

    private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId id) {
        await lobby.Join();
    }

    private void OnLobbyMemberLeave(Lobby lobby, Friend friend){
        UpdatePlayers();
        if(MenuManager.instance != null && NetworkManager.Singleton.IsHost) MenuManager.instance.startButton.SetActive(true); //migrate for starting game
    }

    private void OnLobbyMemberJoined(Lobby lobby, Friend friend){
        UpdatePlayers();
    }

#endregion

    public async void HostLobby() {
        if(string.IsNullOrEmpty(MenuManager.instance.LobbyNameInput.text)) return;
        await SteamMatchmaking.CreateLobbyAsync(100);
    }

    public async void JoinLobbyWithID() { //find lobby with inputed ID, highkey dumb way but Indian guy said so
        ulong ID;
        if (!ulong.TryParse(MenuManager.instance.LobbyIDInputField.text, out ID)) return;

        Lobby[] lobbies = await SteamMatchmaking.LobbyList.WithSlotsAvailable(1).RequestAsync();

        foreach (Lobby lobby in lobbies) { 
            if (lobby.Id == ID) {
                await lobby.Join();
                return;
            }
        }
    }

    public void LeaveLobby() {
        CurrentLobby?.Leave();
        CurrentLobby = null;
        NetworkManager.Singleton.Shutdown();
    }

    public void CopyCode() {
        if(CurrentLobby == null) return;
        TextEditor textEditor = new TextEditor();
        textEditor.text = CurrentLobby?.Id.ToString();
        textEditor.SelectAll();
        textEditor.Copy();
    }

    public void UpdatePlayers() {
        Players = CurrentLobby?.Members.ToList();

        if(MenuManager.instance != null) {
            List<string> names = new List<string>();
            foreach (var player in Players) {
                names.Add(player.Name);
            }
            MenuManager.instance.UpdatePlayerList(names);
        } 
    }

    void OnApplicationQuit()
	{
		CurrentLobby?.Leave();

		if (NetworkManager.Singleton == null)
			return;

		NetworkManager.Singleton.Shutdown();
	}

    public void StartGameServer() {
        if(NetworkManager.Singleton.IsHost) {
            NetworkManager.Singleton.SceneManager.LoadScene("Level1", LoadSceneMode.Single);
        }
    }
}
