using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;
    [SerializeField] private AudioDatabaseSO audioDatabase;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [Space]
    private string currentBGMGroup;
    private Coroutine currentBGMCo;
    [SerializeField] private bool bgmShouldPlay;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!bgmSource.isPlaying && bgmShouldPlay && !string.IsNullOrEmpty(currentBGMGroup))
            NextBGM(currentBGMGroup);

        if (bgmSource.isPlaying && !bgmShouldPlay)
            StopBGM();
    }

   private IEnumerator SwitchBGMCo(string musicGroup)
    {
        AudioClipData data = audioDatabase.Get(musicGroup);
        AudioClip nextTrack = data.GetRandomClip();

        if (data == null || data.clips.Count == 0)
        {
            Debug.Log("No audio tracks found in - " + musicGroup);
            yield break;
        }

        if (bgmSource.isPlaying)
            yield return FadeVolumeCo(bgmSource, 0f, 1f);

        bgmSource.clip = nextTrack;
        bgmSource.volume = 0;
        bgmSource.Play();

        StartCoroutine(FadeVolumeCo(bgmSource, data.volume, 1f));
    }

    private IEnumerator FadeVolumeCo(AudioSource source, float targetVolume, float duration)
    {
        float time = 0;
        float startVolume = source.volume;

        while (time < duration)
        {
            time += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, time / duration);
            yield return null;
        }
        source.volume = targetVolume;
    }

    public void PlaySFX(string soundName, AudioSource sfxSource)
    {
        var data = audioDatabase.Get(soundName);
        if (data == null)
        {
            Debug.Log("Attempted to play sound - " + soundName);
            return;
        }

        var clip = data.GetRandomClip();
        if (clip == null) return;

        sfxSource.clip = clip;
        sfxSource.pitch = Random.Range(data.pitch - 0.05f, data.pitch + 0.05f);
        sfxSource.volume = data.volume;
        sfxSource.PlayOneShot(clip);
    }

    public void StartBGM(string musicGroup)
    {
        bgmShouldPlay = true;

        if (musicGroup == currentBGMGroup)
            return;
        NextBGM(musicGroup);
    }

    public void NextBGM(string musicGroup)
    {
        bgmShouldPlay = true;
        currentBGMGroup = musicGroup;

        if (currentBGMCo != null)
            StopCoroutine(currentBGMCo);
        currentBGMCo = StartCoroutine(SwitchBGMCo(musicGroup));
    }

    public void StopBGM()
    {
        bgmShouldPlay = false;

        StartCoroutine(FadeVolumeCo(bgmSource, 0f, 1f));
        if (currentBGMCo != null)
            StopCoroutine(currentBGMCo);
    }
}
