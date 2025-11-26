using UnityEngine;
using UnityEngine.Audio;

public class AudioSettingsService : MonoBehaviour, IAudioSettingsService
{
    [SerializeField] private AudioMixer mixer;

    private const string MASTER_PARAM = "MasterVol";
    private const string MUSIC_PARAM  = "MusicVol";
    private const string SFX_PARAM    = "SFXVol";

    public static AudioSettingsService instance;

    public float MasterVolume { get; set; }
    public float MusicVolume  { get; set; }
    public float SFXVolume    { get; set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        
        LoadSaved();
        ApplyVolumes();
    }

    public void ApplyVolumes()
    {
        mixer.SetFloat(MASTER_PARAM, LinearToDb(MasterVolume));
        mixer.SetFloat(MUSIC_PARAM,  LinearToDb(MusicVolume));
        mixer.SetFloat(SFX_PARAM,    LinearToDb(SFXVolume));
    }

    private float LinearToDb(float vol) =>
        Mathf.Log10(Mathf.Clamp(vol, 0.0001f, 1f)) * 20f;

    private void LoadSaved()
    {
        MasterVolume = PlayerPrefs.GetFloat(MASTER_PARAM, 1f);
        MusicVolume  = PlayerPrefs.GetFloat(MUSIC_PARAM, 1f);
        SFXVolume    = PlayerPrefs.GetFloat(SFX_PARAM, 1f);
    }

    public void Save()
    {
        PlayerPrefs.SetFloat(MASTER_PARAM, MasterVolume);
        PlayerPrefs.SetFloat(MUSIC_PARAM,  MusicVolume);
        PlayerPrefs.SetFloat(SFX_PARAM,    SFXVolume);
        PlayerPrefs.Save();
    }
}
