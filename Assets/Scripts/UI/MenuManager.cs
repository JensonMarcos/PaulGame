using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public static MenuManager instance;

    [SerializeField] Menu[] menus;
    int prevMenu = 0;


    public TMP_InputField LobbyIDInputField;
    public TextMeshProUGUI LobbyIDDisplay;
    public TMP_InputField LobbyNameInput;
    public TextMeshProUGUI LobbyName;

    [SerializeField] Transform playerListContent;
    [SerializeField] GameObject playerListItemPrefab;
    public GameObject startButton;

    void Awake() {
        instance = this;
    }

    public void OpenMenu(string menuName) {
        for (int i = 0; i < menus.Length; i++)
        {
            if(menus[i].menuName == menuName) {
                menus[i].Open();
            }
            else if(menus[i].open) {
                CloseMenu(menus[i]);
                prevMenu = i;
            }
        }
    }

    public void OpenMenu(Menu menu) {
        for (int i = 0; i < menus.Length; i++)
        {
            if(menus[i].open) {
                CloseMenu(menus[i]);
                prevMenu = i;
            }
        }
        menu.Open();
    }

    public void CloseMenu(Menu menu) {
        menu.Close();
    }

    public void quitGame() {
        Application.Quit();
    }

    public void OpenPreviousMenu() {
        OpenMenu(menus[prevMenu]);
    }

    public void RoomJoin(string ID, string Name, bool Host) {
        LobbyIDDisplay.text = ID;
        LobbyName.text = Name;
        OpenMenu("Room");
        startButton.SetActive(Host);
    }

    public void UpdatePlayerList(List<string> PlayerNames) {
        foreach(Transform trans in playerListContent)
        {
            Destroy(trans.gameObject);
        }
        foreach(string name in PlayerNames) {
            Instantiate(playerListItemPrefab, playerListContent).GetComponent<TextMeshProUGUI>().text = name;
        }
    }

}
