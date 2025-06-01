using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{

    public void Initialize()
    {
        Debug.Log("AudioManager Initialized");
    }

    // Dictionary to store volume levels
    private Dictionary<string, float> volumeLevels = new Dictionary<string, float>()
    {
        {"Master", 1f},
        {"SFX", 1f},
        {"Music", 1f}
    };

    public float GetVolume(string audioType)
    {
        return volumeLevels.ContainsKey(audioType) ? volumeLevels[audioType] : 1f;
    }

    public void SetVolume(string audioType, float volume)
    {
        volume = Mathf.Clamp01(volume);
        volumeLevels[audioType] = volume;

        // Apply the volume to your audio system
        switch (audioType)
        {
            case "Master":
                AudioListener.volume = volume;
                break;
            case "SFX":
                // Apply to SFX AudioSource or AudioMixerGroup
                break;
            case "Music":
                // Apply to Music AudioSource or AudioMixerGroup
                break;
        }

        Debug.Log($"Set {audioType} volume to {volume}");
    }

}
