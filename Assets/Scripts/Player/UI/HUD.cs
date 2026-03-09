using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [SerializeField] Image bar;

    [SerializeField] TMP_Text healthText, hpText, deadText;

    [SerializeField] float flashDuration;

    bool isDead;

    public void UpdateHealth(float health)
    {
        hpText.text = Mathf.CeilToInt(health).ToString();
        StartCoroutine(FlashRed());
    }

    public void SetDead(bool _isDead)
    {
        isDead = _isDead;
        StopCoroutine("FlashRed");

        deadText.gameObject.SetActive(isDead);
        healthText.gameObject.SetActive(!isDead);
        hpText.gameObject.SetActive(!isDead);
        bar.color = isDead ? Color.red : Color.blue;

        if(!isDead) hpText.text = "100";
    }

    IEnumerator FlashRed()
    {
        bar.color = Color.red;
        yield return new WaitForSeconds(flashDuration);
        if(!isDead) bar.color = Color.blue;
    }
}
