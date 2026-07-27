using System.Diagnostics;

public class LinuxGPUProvider : IGPUProvider
{
    public float GetGPUUsagePercent()
    {
        try
        {
            ProcessStartInfo info =
                new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments =
                        "--query-gpu=utilization.gpu " +
                        "--format=csv,noheader,nounits",

                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

            using Process process = Process.Start(info);
            string output = process.StandardOutput.ReadToEnd();

            process.WaitForExit();

            if (float.TryParse(output.Trim(), out float usage))
                return usage;
        }
        catch
        { }
        return -1f;
    }

    public string GetGPUName()
    {
        return "Linux GPU";
    }
}
