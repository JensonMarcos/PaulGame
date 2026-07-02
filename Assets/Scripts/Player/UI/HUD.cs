using System.Collections;
using TMPro;
using UnityEngine;
using Unity.Collections;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public TMP_Text gameTitle;

    [SerializeField] TMP_Text healthText, hpText, deadText;
    [SerializeField] Image bar;
    [SerializeField] float flashDuration;

    bool isDead;

    Coroutine flashCoroutine;

    public void UpdateHealth(float health)
    {
        hpText.text = Mathf.CeilToInt(health).ToString();
        if(health < 100)
        {
            if(flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRed());
        }
    }

    public void SetDead(bool _isDead)
    {
        isDead = _isDead;
        if(flashCoroutine != null) StopCoroutine(flashCoroutine);

        deadText.gameObject.SetActive(isDead);
        healthText.gameObject.SetActive(!isDead);
        hpText.gameObject.SetActive(!isDead);
        bar.color = isDead ? Color.red : Color.blue;

        if(!isDead) hpText.text = "100";
    }

    public void OnTitleChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        gameTitle.text = current.ToString();
    }

    IEnumerator FlashRed()
    {
        bar.color = Color.red;
        yield return new WaitForSeconds(flashDuration);
        if(!isDead) bar.color = Color.blue;
    }
}
