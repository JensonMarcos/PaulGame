using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[Serializable]
public struct SoundData : INetworkSerializable
{
    public int clipID;
    //public AudioMixerGroup mixerGroup;
    public bool loop;
    public float volume;
    public float pitchDeviation;
    public float spatialBlend;
    public bool play2DLocal;
    public float minDistance;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clipID);
        serializer.SerializeValue(ref loop);
        serializer.SerializeValue(ref volume);
        serializer.SerializeValue(ref pitchDeviation);
        serializer.SerializeValue(ref spatialBlend);
        serializer.SerializeValue(ref play2DLocal);
        serializer.SerializeValue(ref minDistance);
    }
}

public class SoundManager : NetworkBehaviour
{
    public static SoundManager instance;

    [SerializeField] SoundEmitter soundEmitterPrefab;
    ObjectPool<SoundEmitter> soundEmitterPool;
    
    public SoundList SoundList;

    void Awake()
    {
        instance = this;

        soundEmitterPool = new ObjectPool<SoundEmitter>(() => {
            var obj = Instantiate(soundEmitterPrefab);
            obj.gameObject.SetActive(false);
            return obj;
        }, actionOnGet: (obj) => {
            obj.gameObject.SetActive(true);
        }, actionOnRelease: (obj) => {
            obj.gameObject.SetActive(false);
        }, actionOnDestroy: (obj) => {
            if(obj == null) return;
            if(Application.isPlaying) Destroy(obj.gameObject);
            else DestroyImmediate(obj.gameObject);
        }, collectionCheck: false, defaultCapacity: 10, maxSize: 100);
    }

    public void ReturnToPool(SoundEmitter emitter)
    {
        soundEmitterPool.Release(emitter);
    }

    public void PlaySound(SoundData soundData, Vector3 position, bool play2D)
    {
        SoundEmitter emitter = soundEmitterPool.Get();
        emitter.Initialize(soundData, position);
        emitter.Play(play2D);
    }

    public void PlayNetworkSound(SoundData soundData, Vector3 position)
    {
        bool play2D = soundData.play2DLocal;
        PlaySoundServerRpc(soundData, position);
        PlaySound(soundData, position, play2D);
    }

    [Rpc(SendTo.Server)] 
    public void PlaySoundServerRpc(SoundData soundData, Vector3 position, RpcParams rpcParams = default) {
        ulong senderClientId = rpcParams.Receive.SenderClientId;

        PlaySoundClientRpc(soundData, position, RpcTarget.Not(senderClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)] 
    public void PlaySoundClientRpc(SoundData soundData, Vector3 position, RpcParams rpcParams = default) {
        PlaySound(soundData, position, false);
    }


}
