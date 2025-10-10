//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;

namespace MyndSproutApp
{
    internal static class ConnectionStringHelper
    {
        public static string BuildFromViewModel(MainViewModel vm)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));

            // If the user typed a full connection string, honor it.
            if (!string.IsNullOrWhiteSpace(vm.ConnectionString))
                return vm.ConnectionString!;

            string db = string.IsNullOrWhiteSpace(vm.DatabaseName) ? "AgenticDb" : vm.DatabaseName;

            // If a server-level string was provided, ensure it has Database=
            if (!string.IsNullOrWhiteSpace(vm.ServerConnectionString))
            {
                var s = vm.ServerConnectionString!.Trim();
                if (s.IndexOf("Database=", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.IndexOf("Initial Catalog=", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    if (!s.EndsWith(";")) s += ";";
                    s += $"Database={db};";
                }
                return s;
            }

            // Default: LocalDB with trusted connection
            return $"Server=(localdb)\\MSSQLLocalDB;Database={db};Trusted_Connection=True;TrustServerCertificate=True;";
        }
    }
}
