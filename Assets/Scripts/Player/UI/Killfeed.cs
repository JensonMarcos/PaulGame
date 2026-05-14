using System.Collections;
using UnityEngine;

public class Killfeed : MonoBehaviour
{
    [SerializeField] GameObject killfeedItemPrefab;

    [SerializeField] float itemLifetime, decayDelay, backgroundLengthRatio, backgroundLengthInit;//, opacity, length;

    public void AddKillfeedItem(string text, bool clientIncluded)
    {
        KillfeedItem item = Instantiate(killfeedItemPrefab, transform).GetComponent<KillfeedItem>();
        item.killText.text = text;
        int num_chars = text.Length;
        item.line.rectTransform.localScale = new Vector3(num_chars*backgroundLengthRatio+backgroundLengthInit, 1f, 1f);

        if(clientIncluded) //clientIncluded state is just the default prefab state im lazy  <- wtf does this mean
        {
            // item.gradient.color = new Color(item.gradient.color.r, item.gradient.color.g, item.gradient.color.b, opacity);
            // item.gradient.rectTransform.localScale = new Vector3(length, 1f, 1f);
            item.line.color = Color.white;
            item.killText.color = Color.blue;
        }

        StartCoroutine(KillfeedItemDecay(item));
    }

    IEnumerator KillfeedItemDecay(KillfeedItem item)
    {
        float timer = itemLifetime;
        while(timer > 0f)
        {
            item.canvasGroup.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp(decayDelay*timer/itemLifetime, 0f, 1f));
            timer -= Time.deltaTime;
            yield return null;
        }
        Destroy(item.gameObject);
    }

}
