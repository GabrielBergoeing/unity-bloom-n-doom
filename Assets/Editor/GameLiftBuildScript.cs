using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// CLI entry point for building the headless dedicated server that gets uploaded to AWS
// GameLift as a Build resource. Run from the command line with:
//   Unity.exe -batchmode -quit -nographics -projectPath <repo> -executeMethod GameLiftBuildScript.BuildLinuxServer -logFile <path>
// Uses the same enabled-scenes list as the regular player build (File > Build Settings),
// so CharacterSelectorOnline.unity - the scene GameManager.Start() jumps straight to
// under UNITY_SERVER, see Assets/Scripts/GameManager.cs - just needs to stay checked
// there like it already is.
public static class GameLiftBuildScript
{
    public static void BuildLinuxServer()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[GameLiftBuildScript] No hay escenas habilitadas en Build Settings.");
            EditorApplication.Exit(1);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/GameLiftServer/BloomAndDoomServer",
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.None
        };

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.LogError($"[GameLiftBuildScript] Build falló: {report.summary.result} ({report.summary.totalErrors} errores).");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[GameLiftBuildScript] Build OK -> {options.locationPathName} ({report.summary.totalSize} bytes).");
        EditorApplication.Exit(0);
    }
}
