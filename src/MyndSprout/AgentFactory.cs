//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
using System;
using Microsoft.Data.SqlClient;

namespace MyndSprout
{
    /// <summary>
    /// Central place to construct SqlAgent instances.
    /// SqlAgent has no public constructors; use these methods.
    /// </summary>
    public static class AgentFactory
    {
        public static async Task<SqlAgent> CreateAgentAsync(string serverConnectionString, string dbName, int maxEpochs = 5)
        {
            var sql = new SqlStrings();

            string createXml =
                $"<CreateDatabaseInput><ServerConnectionString>{XmlEscape(serverConnectionString)}</ServerConnectionString><DbName>{XmlEscape(dbName)}</DbName></CreateDatabaseInput>";
            var createRes = await sql.CreateDatabaseAsyncStr(createXml).ConfigureAwait(false);
            // (optional) verify success="true" in createRes

            var builder = new SqlConnectionStringBuilder(serverConnectionString) { InitialCatalog = dbName };
            string connectXml =
                $"<ConnectInput><ConnectionString>{XmlEscape(builder.ConnectionString)}</ConnectionString></ConnectInput>";
            var connRes = await sql.ConnectAsyncStr(connectXml).ConfigureAwait(false);
            // (optional) verify success="true" in connRes

            return new SqlAgent(sql, maxEpochs);
        }

        public static async Task<SqlAgent> CreateAgentFromConnectionStringAsync(string connectionString, int maxEpochs = 5)
        {
            var sql = new SqlStrings();
            string connectXml = $"<ConnectInput><ConnectionString>{XmlEscape(connectionString)}</ConnectionString></ConnectInput>";
            var connRes = await sql.ConnectAsyncStr(connectXml).ConfigureAwait(false);
            return new SqlAgent(sql, maxEpochs);
        }

        public static Task<SqlAgent> CreateAgentWithDefaultServerAsync(string dbName, int maxEpochs = 5, string? overrideDefaultServerConnStr = null)
        {
            string serverConnStr = ResolveDefaultServerConnectionString(overrideDefaultServerConnStr);
            return CreateAgentAsync(serverConnStr, dbName, maxEpochs);
        }

        /// <summary>
        /// Create (if missing) and connect to a database using a DEFAULT server connection string.
        /// Default can be overridden by env vars: MYNDSPROUT_SERVER_CONNSTR or MYNDSPROUT_CONNSTR,
        /// or by passing overrideDefaultServerConnStr.
        /// Fallback: (localdb)\MSSQLLocalDB with Integrated Security.
        /// </summary>
        public async static Task<SqlAgent> CreateAgentWithDefaultServer(string dbName, int maxEpochs = 5, string? overrideDefaultServerConnStr = null)
        {
            string serverConnStr = ResolveDefaultServerConnectionString(overrideDefaultServerConnStr);
            return await CreateAgentAsync(serverConnStr, dbName, maxEpochs);
        }

        /// <summary>
        /// Build an agent from a pre-configured SqlStrings (already connected or not).
        /// </summary>
        public static SqlAgent CreateAgent(SqlStrings? sqlStrings, int maxEpochs = 5)
        {
            if (sqlStrings is null) throw new ArgumentNullException(nameof(sqlStrings));
            return new SqlAgent(sqlStrings, maxEpochs);
        }

        // ---------------------------
        // Helpers
        // ---------------------------

        private static string ResolveDefaultServerConnectionString(string? overrideDefault)
        {
            if (!string.IsNullOrWhiteSpace(overrideDefault)) return overrideDefault!;
            var env =
                Environment.GetEnvironmentVariable("MYNDSPROUT_SERVER_CONNSTR") ??
                Environment.GetEnvironmentVariable("MYNDSPROUT_CONNSTR");
            if (!string.IsNullOrWhiteSpace(env)) return env!;
            return "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";
        }

        private static string XmlEscape(string s)
            => string.IsNullOrEmpty(s)
                ? string.Empty
                : s.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;")
                   .Replace("'", "&apos;");
    }
}

