using UnityEngine;

public static class GPUProviderFactory
{
    public static IGPUProvider Create()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                return new WindowsGPUProvider();

            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxEditor:
                return new LinuxGPUProvider();

            default:
                return new NullGPUProvider();
        }
    }
}
