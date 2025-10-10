//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.Data.SqlClient;
using System.Runtime.ExceptionServices;

namespace SqlContain;

public static class Hardener
{
    public static async Task<int> RunAsync(HardenerOptions options, Action<string>? log = null)
    {
        options.Validate();

        // Firewall-only branch (when host EXE relaunches elevated)
        if (options.InternalFirewallOnly)
        {
            try
            {
                Platform.EnsureWindows();
                Platform.EnsureElevatedOrThrow();
                FirewallService.ApplyOutboundBlock(options.SqlServrPath);
                log?.Invoke("Firewall applied.");
                return 0;
            }
            catch (Exception ex)
            {
                log?.Invoke("Firewall step failed: " + ex.Message);
                return 1;
            }
        }

        try
        {
            using var master = SqlConnectionFactory.CreateMaster(options);
            await master.OpenAsync();

            var isSysAdmin = await SqlHelpers.ExecScalarAsync<int>(master, "SELECT IS_SRVROLEMEMBER('sysadmin');");
            if (isSysAdmin != 1) throw new Exception("Login must be in 'sysadmin' role.");

            if (options.Scope is Scope.Instance or Scope.Both)
            {
                log?.Invoke("Hardening instance...");
                await ServerHardener.HardenAsync(master);
                log?.Invoke("Instance hardened.");
            }

            if (options.Scope is Scope.Database or Scope.Both)
            {
                log?.Invoke($"Hardening database [{options.Database}]...");
                await DatabaseHardener.HardenAsync(master, options.Database!, options);
                log?.Invoke("Database hardened.");
            }

            if (options.Firewall)
            {
                log?.Invoke("Configuring firewall (UAC may prompt)...");
                var ok = await FirewallService.EnsureWithElevationAsync(options.SqlServrPath);
                log?.Invoke(ok ? "Firewall configured." : "Firewall skipped/failed.");
            }

            log?.Invoke("All requested steps complete.");
            return 0;
        }
        catch (AggregateException aggEx)
        {
            // Preserve AggregateException semantics so callers/tests can observe failures.
            ExceptionDispatchInfo.Capture(aggEx).Throw();
            return 1;
        }
        catch (Exception ex)
        {
            log?.Invoke("Hardening failed: " + ex.Message);
            return 1;
        }
    }
}
