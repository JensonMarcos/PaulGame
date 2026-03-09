using System.Collections;
using UnityEngine;

public class Killfeed : MonoBehaviour
{
    [SerializeField] GameObject killfeedItemPrefab;

    [SerializeField] float itemLifetime, decayDelay, opacity, length;

    public void AddKillfeedItem(string text, bool clientIncluded)
    {
        KillfeedItem item = Instantiate(killfeedItemPrefab, transform).GetComponent<KillfeedItem>();
        item.killText.text = text;

        if(!clientIncluded) //clientIncluded state is just the default prefab state im lazy
        {
            item.gradient.color = new Color(item.gradient.color.r, item.gradient.color.g, item.gradient.color.b, opacity);
            item.gradient.rectTransform.localScale = new Vector3(length, 1f, 1f);
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
