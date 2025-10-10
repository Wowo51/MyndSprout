//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SqlContain;

internal static class Platform
{
    public static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Firewall block is supported on Windows only.");
    }

    public static bool IsProcessElevated()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        using var id = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(id);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void EnsureElevatedOrThrow()
    {
        if (!IsProcessElevated())
            throw new UnauthorizedAccessException("Administrator privileges are required.");
    }
}
