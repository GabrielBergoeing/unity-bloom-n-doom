using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Audio Database")]
public class AudioDatabaseSO : ScriptableObject
{
    public List<AudioClipData> playerAudio;
    public List<AudioClipData> UIAudio;
    public List<AudioClipData> backgroundAudio;
    public List<AudioClipData> toolAudio;
    public List<AudioClipData> plantAudio;

    private Dictionary<string, AudioClipData> clipCollection;

    private void OnEnable()
    {
        clipCollection = new Dictionary<string, AudioClipData>();

        AddClipToCollection(playerAudio);
        AddClipToCollection(UIAudio);
        AddClipToCollection(backgroundAudio);
        AddClipToCollection(toolAudio);
        AddClipToCollection(plantAudio);
    }

    private void AddClipToCollection(List<AudioClipData> listToAdd)
    {
        foreach (var data in listToAdd)
        {
            if (data != null && !clipCollection.ContainsKey(data.audioName))
            {
                clipCollection.Add(data.audioName, data);
            }
        }
    }

    public AudioClipData Get(string groupName)
    {
        return clipCollection.TryGetValue(groupName, out var data) ? data : null;
    }

}

[System.Serializable]
public class AudioClipData
{
    public string audioName;
    public List<AudioClip> clips = new List<AudioClip>();
    [Range(0f, 1f)] public float volume;
    [Range(0f, 2f)] public float pitch = 1;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Count == 0)
            return null;

        return clips[Random.Range(0, clips.Count)];
    }
}