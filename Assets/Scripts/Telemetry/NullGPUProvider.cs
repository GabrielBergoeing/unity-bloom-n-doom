public class NullGPUProvider : IGPUProvider
{
    public float GetGPUUsagePercent()
    {
        return -1f;
    }

    public string GetGPUName()
    {
        return "Unsupported";
    }
}
