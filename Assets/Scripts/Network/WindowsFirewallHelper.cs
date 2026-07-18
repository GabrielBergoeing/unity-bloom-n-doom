using System;
using UnityEngine;

// Best-effort: opens the game's UDP port in Windows Defender Firewall when hosting.
// Windows normally shows its own "Firewall blocked some features of this app, allow?"
// popup automatically the first time an app listens on a port - but that notification
// is disabled by default on the "Public" network category, so on that profile Windows
// silently drops all inbound traffic instead, with zero indication anything is wrong
// (this is what took a long debugging session to track down). Router/NAT traversal
// (UPnP, hole punching) is a completely separate problem this doesn't overlap with -
// this only handles the local Windows Firewall layer.
//
// No-op on any non-Windows platform, and in the Editor (matches UpnpPortMapper's and
// HolePunchClient's own Editor skip - local/ParrelSync testing doesn't need this).
public static class WindowsFirewallHelper
{
    private const string RuleNamePrefix = "BloomAndDoom-GamePort-";

    public static void EnsureInboundUdpRule(ushort port)
    {
#if UNITY_STANDALONE_WIN
        if (Application.isEditor)
            return;

        // RuleExists()/AddRuleElevated() shell out to netsh and can block for a while -
        // AddRuleElevated specifically waits on the player's UAC response (up to 15s) -
        // so this runs off the main thread to avoid visibly freezing the game while
        // hosting starts. Debug.Log is safe to call from a background thread in Unity.
        System.Threading.Tasks.Task.Run(() =>
        {
            string ruleName = RuleNamePrefix + port;

            if (RuleExists(ruleName))
            {
                Debug.Log($"[WindowsFirewallHelper] La regla de firewall para el puerto {port} ya existe.");
                return;
            }

            Debug.Log($"[WindowsFirewallHelper] Pidiendo permiso para abrir el puerto {port}/UDP en el firewall de Windows...");
            AddRuleElevated(ruleName, port);
        });
#endif
    }

#if UNITY_STANDALONE_WIN
    private static bool RuleExists(string ruleName)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            // Locale-independent: our own rule name only shows up in the output if
            // netsh actually found a matching rule, regardless of which language
            // Windows displays the surrounding labels in.
            return output.Contains(ruleName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WindowsFirewallHelper] No se pudo consultar reglas de firewall: {ex.Message}");
            return true; // fail safe: assume it exists so we don't repeatedly prompt on error
        }
    }

    private static void AddRuleElevated(string ruleName, ushort port)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=UDP localport={port}",
                    UseShellExecute = true,
                    Verb = "runas", // triggers a single UAC prompt for just this command
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                }
            };
            process.Start();
            process.WaitForExit(15000);

            Debug.Log(process.ExitCode == 0
                ? $"[WindowsFirewallHelper] Regla de firewall creada para el puerto {port}/UDP."
                : $"[WindowsFirewallHelper] La creación de la regla no terminó con éxito (exit code {process.ExitCode}).");
        }
        catch (Exception ex)
        {
            // Most common cause: the player clicked "No" on the UAC prompt - that
            // throws (Win32Exception, ERROR_CANCELLED) rather than returning a normal
            // exit code, so it's caught here instead of read from ExitCode above.
            Debug.LogWarning($"[WindowsFirewallHelper] No se pudo crear la regla de firewall (¿se rechazó el permiso?): {ex.Message}");
        }
    }
#endif
}
