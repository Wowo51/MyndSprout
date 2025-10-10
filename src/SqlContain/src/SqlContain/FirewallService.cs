//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Diagnostics;

namespace SqlContain;

internal static class FirewallService
{
    public static void ApplyOutboundBlock(string? sqlservrPath)
    {
        if (string.IsNullOrWhiteSpace(sqlservrPath))
            throw new ArgumentException("sqlservrPath is required to apply outbound firewall block and must not be empty.", nameof(sqlservrPath));
        var path = sqlservrPath!;
        var ruleName = "SQLServer_Block_All_Outbound";

        RunHidden("netsh", $"advfirewall firewall delete rule name=\"{ruleName}\" program=\"{path}\" dir=out");
        RunHidden("netsh", $"advfirewall firewall add rule name=\"{ruleName}\" dir=out program=\"{path}\" action=block enable=yes");
    }

    public static async Task<bool> EnsureWithElevationAsync(string? sqlservrPath)
    {
        Platform.EnsureWindows();

        if (Platform.IsProcessElevated())
        {
            ApplyOutboundBlock(sqlservrPath);
            return true;
        }

        // Relaunch host EXE elevated for firewall-only mode
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate host executable.");
        var args = "--internal-firewall-only true";
        if (!string.IsNullOrWhiteSpace(sqlservrPath)) args += $" --sqlservr-path \"{sqlservrPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = true,
            Verb = "runas", // UAC
            Arguments = args
        };

        try
        {
            using var p = Process.Start(psi);
            if (p == null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User canceled UAC
            return false;
        }
    }

    private static void RunHidden(string file, string args)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        p.Start();
        p.WaitForExit();
    }
}
