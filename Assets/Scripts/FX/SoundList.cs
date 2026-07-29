using UnityEngine;

[CreateAssetMenu(fileName = "SoundList", menuName = "Scriptable Objects/SoundList")]
public class SoundList : ScriptableObject
{
    public AudioClip[] Clips;
}
