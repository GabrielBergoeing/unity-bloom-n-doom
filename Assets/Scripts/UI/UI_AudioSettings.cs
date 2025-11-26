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

        masterSlider.onValueChanged.AddListener(v => audioSvc.MasterVolume = v);
        musicSlider.onValueChanged.AddListener(v => audioSvc.MusicVolume  = v);
        sfxSlider.onValueChanged.AddListener(v => audioSvc.SFXVolume      = v);
    }

    public void ApplyChanges()
    {
        audioSvc.ApplyVolumes();
        (audioSvc as AudioSettingsService)?.Save();
        UIService.instance.sfx.PlayOnConfirm(); // keep your UX feedback!
    }
}
