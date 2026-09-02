using System.Collections;
using UnityEngine;

public class SoundEmitter : MonoBehaviour
{
    [SerializeField] AudioSource audioSource;

    Transform follow;
    Coroutine lifetime;

    public void Play(SoundEntry entry, byte variant, Vector3 position, Transform follow, bool play2D)
    {
        this.follow = follow;
        transform.position = position;

        audioSource.clip = entry.clips[variant];
        audioSource.outputAudioMixerGroup = entry.group;
        audioSource.loop = entry.loop;
        audioSource.volume = entry.volume;
        audioSource.pitch = entry.pitch + entry.pitchDeviation * Random.Range(-1f, 1f);
        audioSource.spatialBlend = play2D ? 0f : 1f;
        audioSource.minDistance = entry.minDistance;
        audioSource.maxDistance = entry.maxDistance;
        audioSource.Play();

        if (lifetime != null) StopCoroutine(lifetime);
        lifetime = entry.loop ? null : StartCoroutine(ReleaseWhenFinished());
    }

    public void Stop()
    {
        if (!gameObject.activeSelf) return;

        if (lifetime != null) StopCoroutine(lifetime);
        lifetime = null;
        audioSource.Stop();
        SoundManager.instance.Release(this);
    }

    void LateUpdate()
    {
        if (follow != null) transform.position = follow.position;
    }

    IEnumerator ReleaseWhenFinished()
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        lifetime = null;
        SoundManager.instance.Release(this);
    }
}
