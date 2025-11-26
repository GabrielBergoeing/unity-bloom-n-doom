public interface IAudioSettingsService
{
    float MasterVolume { get; set; }
    float MusicVolume  { get; set; }
    float SFXVolume    { get; set; }

    void ApplyVolumes();
}
