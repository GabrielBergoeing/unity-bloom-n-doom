using UnityEngine;
using UnityEngine.UI;

public class UI_AudioSettings : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private IAudioSettingsService audioSvc;

    private void Start()
    {
        audioSvc = AudioSettingsService.instance;

        masterSlider.value = audioSvc.MasterVolume;
        musicSlider.value  = audioSvc.MusicVolume;
        sfxSlider.value    = audioSvc.SFXVolume;

        masterSlider.onValueChanged.AddListener(OnMasterChanged);
        musicSlider.onValueChanged.AddListener(OnMusicChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxChanged);
    }

    private void OnMasterChanged(float v)
    {
        audioSvc.MasterVolume = v;
        audioSvc.ApplyVolumes();
    }

    private void OnMusicChanged(float v)
    {
        audioSvc.MusicVolume = v;
        audioSvc.ApplyVolumes();
    }

    private void OnSfxChanged(float v)
    {
        audioSvc.SFXVolume = v;
        audioSvc.ApplyVolumes();
    }
}
