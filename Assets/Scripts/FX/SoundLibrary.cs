using System;
using UnityEngine;
using UnityEngine.Audio;

public enum SoundMode
{
    Local2D,         // only the caller hears it, no position
    Local3D,         // only the caller hears it, positioned
    Networked,       // everyone hears it, positioned
    NetworkedSelf2D  // everyone hears it positioned, the caller hears it 2D
}

[Serializable]
public class SoundEntry
{
    public string name;
    public AudioClip[] clips;
    public SoundMode mode = SoundMode.Networked;
    public AudioMixerGroup group;
    [Range(0f, 1f)] public float volume = 1f;
    public float pitch = 1f;
    public float pitchDeviation = 0.05f;
    public bool loop;
    public float minDistance = 1f;
    public float maxDistance = 500f;

    public bool IsNetworked => mode is SoundMode.Networked or SoundMode.NetworkedSelf2D;
    public bool IsSelf2D => mode is SoundMode.Local2D or SoundMode.NetworkedSelf2D;
}

[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Scriptable Objects/SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    public SoundEntry[] sounds;
}
