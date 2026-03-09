using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Scoreboard : MonoBehaviour
{
    public List<ScoreboardItem> items = new List<ScoreboardItem>();
    [SerializeField] Transform layoutGroup;
    [SerializeField] GameObject scorboardItemPrefab;
    [SerializeField] GameObject WKD;
    [SerializeField] int falloff;

    bool tabPressed;

    public void InputUpdate(bool _tabPressed)
    {
        tabPressed = _tabPressed;

        if(tabPressed)
        {
            foreach (ScoreboardItem item in items)
            {
                item.gradient.color = new Color(item.gradient.color.r, item.gradient.color.g, item.gradient.color.b, 0.67f);
                item.gradient.rectTransform.localScale = Vector3.one;

                item.canvasGroup.alpha = 1f;

                item.winsText.alpha = item.killsText.alpha = item.deathsText.alpha = 1f;

                WKD.SetActive(true);
            }
        }
        else
        {
            int fallofftemp = falloff;
            foreach (ScoreboardItem item in items)
            {
                item.gradient.color = new Color(item.gradient.color.r, item.gradient.color.g, item.gradient.color.b, 0.33f + 0.33f * fallofftemp/falloff);
                item.gradient.rectTransform.localScale = new Vector3(0.5f + 0.5f * fallofftemp/falloff, 1f, 1f);

                item.canvasGroup.alpha = (float)fallofftemp/falloff;

                if(fallofftemp > 0) fallofftemp--;

                item.winsText.alpha = item.killsText.alpha = item.deathsText.alpha = 0f;

                WKD.SetActive(false);
            }
        }
    }

    public void AddItem(ulong playerId, string playerName, int wins, int kills, int deaths)
    {
        ScoreboardItem item = Instantiate(scorboardItemPrefab, layoutGroup).GetComponent<ScoreboardItem>();
        item.playerId = playerId;
        item.wins = wins;

        item.playerNameText.text = playerName;
        item.winsText.text = wins.ToString();
        item.killsText.text = kills.ToString();
        item.deathsText.text = deaths.ToString();

        items.Add(item);
        
        InputUpdate(tabPressed);
    }

    public void RemoveItem(ulong playerId)
    {
        ScoreboardItem item = items.Find(x => x.playerId == playerId);
        if (item != null)
        {
            items.Remove(item);
            Destroy(item.gameObject);
        }
    }

    public void UpdateItem(ulong playerId, int wins, int kills, int deaths)
    {
        ScoreboardItem item = items.Find(x => x.playerId == playerId);
        if (item != null)
        {
            item.wins = wins;
            item.winsText.text = wins.ToString();
            item.killsText.text = kills.ToString();
            item.deathsText.text = deaths.ToString();
        }
        SortItems();
    }

    void SortItems()
    {
        items.Sort((a, b) => b.wins.CompareTo(a.wins));
        for (int i = 0; i < items.Count; i++)
        {
            items[i].transform.SetSiblingIndex(i);
            items[i].placeText.text = (i + 1).ToString();
        }
    }

    // void Update()
    // {
    //     #if UNITY_EDITOR
    //     if(Keyboard.current.jKey.wasPressedThisFrame) {
    //         SortItems();
    //     }
    //     #endif
    // }
}
