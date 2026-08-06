using System.Collections;
using UnityEngine;

public class SoundEmitter : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;
    Coroutine playingCoroutine;

    public void Initialize(SoundData soundData, Vector3 position, Transform parent = null)
    {
        transform.position = position;
        if(parent != null) transform.SetParent(parent);

        audioSource.clip = SoundManager.instance.SoundList.Clips[soundData.clipID];
        audioSource.loop = soundData.loop;
        audioSource.volume = soundData.volume;
        audioSource.pitch = 1 + soundData.pitchDeviation * Random.Range(-1f, 1f);
        audioSource.spatialBlend = soundData.spatialBlend;
        audioSource.minDistance = soundData.minDistance;
    }

    public void Play(bool play2D)
    {
        if(playingCoroutine != null) StopCoroutine(playingCoroutine);

        if(play2D) audioSource.spatialBlend = 0f;
        audioSource.Play();
        playingCoroutine = StartCoroutine(WaitForSoundToEnd());
    }

    public void Stop()
    {
        if(playingCoroutine != null)
        {
            StopCoroutine(playingCoroutine);
            playingCoroutine = null;
        } 

        audioSource.Stop();
        SoundManager.instance.ReturnToPool(this);
    }

    IEnumerator WaitForSoundToEnd()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        SoundManager.instance.ReturnToPool(this);
    }
}
