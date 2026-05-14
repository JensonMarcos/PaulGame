using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreboardItem : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public Image bg, line;
    public TMP_Text placeText, playerNameText, winsText, killsText, deathsText;

    [Space]
    public ulong playerId;
    public int wins;
    public int kills;
}
