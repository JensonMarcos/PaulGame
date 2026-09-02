using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

public class SoundManager : NetworkBehaviour
{
    public static SoundManager instance;

    [SerializeField] SoundLibrary library;
    [SerializeField] SoundEmitter emitterPrefab;
    [SerializeField] AudioMixer mixer;

    readonly Dictionary<string, ushort> ids = new();
    readonly HashSet<string> warned = new();
    ObjectPool<SoundEmitter> pool;

    void Awake()
    {
        instance = this;

        for (ushort i = 0; i < library.sounds.Length; i++)
            ids[library.sounds[i].name] = i;

        pool = new ObjectPool<SoundEmitter>(() => {
            SoundEmitter emitter = Instantiate(emitterPrefab, transform);
            emitter.gameObject.SetActive(false);
            return emitter;
        }, actionOnGet: emitter => {
            emitter.gameObject.SetActive(true);
        }, actionOnRelease: emitter => {
            emitter.gameObject.SetActive(false);
            emitter.transform.SetParent(transform);
        }, actionOnDestroy: emitter => {
            if (emitter == null) return;
            if (Application.isPlaying) Destroy(emitter.gameObject);
            else DestroyImmediate(emitter.gameObject);
        }, collectionCheck: false, defaultCapacity: 32, maxSize: 200);
    }

    public static SoundEmitter Play(string name) => instance.PlayInternal(name, Vector3.zero, null);
    public static SoundEmitter Play(string name, Vector3 position) => instance.PlayInternal(name, position, null);
    public static SoundEmitter Play(string name, Transform follow) => instance.PlayInternal(name, follow.position, follow);

    public static void SetVolume(string param, float linear01) =>
        instance.mixer.SetFloat(param, Mathf.Log10(Mathf.Max(linear01, 0.0001f)) * 20f);

    SoundEmitter PlayInternal(string name, Vector3 position, Transform follow)
    {
        if (!TryGetId(name, out ushort id)) return null;

        SoundEntry entry = library.sounds[id];
        byte variant = (byte)Random.Range(0, entry.clips.Length);

        if (entry.IsNetworked) PlayServerRpc(id, variant, position);

        return Spawn(entry, variant, position, follow, entry.IsSelf2D);
    }

    bool TryGetId(string name, out ushort id)
    {
        id = 0;
        if (string.IsNullOrEmpty(name)) return false;
        if (ids.TryGetValue(name, out id)) return true;

        if (warned.Add(name)) Debug.LogWarning($"No sound named '{name}' in {library.name}");
        return false;
    }

    SoundEmitter Spawn(SoundEntry entry, byte variant, Vector3 position, Transform follow, bool play2D)
    {
        if (entry.clips.Length == 0) return null;

        SoundEmitter emitter = pool.Get();
        emitter.Play(entry, variant, position, follow, play2D);
        return emitter;
    }

    public void Release(SoundEmitter emitter) => pool.Release(emitter);

    [Rpc(SendTo.Server)]
    void PlayServerRpc(ushort id, byte variant, Vector3 position, RpcParams rpcParams = default) =>
        PlayClientRpc(id, variant, position, RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));

    [Rpc(SendTo.SpecifiedInParams)]
    void PlayClientRpc(ushort id, byte variant, Vector3 position, RpcParams rpcParams = default) =>
        Spawn(library.sounds[id], variant, position, null, false);
}
