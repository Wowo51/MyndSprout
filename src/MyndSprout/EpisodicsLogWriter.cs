//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyndSprout
{
    /// <summary>
    /// Hard-gated sink for dbo.Episodics.
    /// - Use a dedicated connection string for the second (writer) call.
    /// - Call RunBootstrapOnceAsync(...) at startup or at the top of SqlAgent.RunAsync to ensure roles/proc/grants.
    /// </summary>
    public sealed class EpisodicsLogWriter : IAsyncDisposable
    {
        // --- Config defaults (override via constructor if you like) ---
        public const string DefaultProcName = "dbo.usp_SaveEpisodic";
        public const string RoleWriter = "role_episodics_writer";
        public const string RoleLlm = "role_llm_generated_sql";

        private readonly SqlConnection _writerConn;
        private readonly string _procName;

        public EpisodicsLogWriter(string writerConnectionString, string procName = DefaultProcName)
        {
            if (string.IsNullOrWhiteSpace(writerConnectionString))
                throw new ArgumentNullException(nameof(writerConnectionString));

            _writerConn = new SqlConnection(writerConnectionString);
            _procName = procName;
        }

        /// <summary>
        /// Write one episodic row via stored procedure (second call in the epoch).
        /// Assumes the caller already separated principals so the LLM batch cannot write to Episodics.
        /// </summary>
        public async Task SaveAsync(EpisodicRecord r, CancellationToken ct = default)
        {
            if (_writerConn.State != ConnectionState.Open) await _writerConn.OpenAsync(ct);

            await using var cmd = _writerConn.CreateCommand();
            cmd.CommandText = _procName;
            cmd.CommandType = CommandType.StoredProcedure;

            var id = r.EpisodeId == Guid.Empty ? Guid.NewGuid() : r.EpisodeId;
            var now = r.Time == default ? DateTime.UtcNow : r.Time;

            cmd.Parameters.AddWithValue("@EpisodeId", id);
            cmd.Parameters.AddWithValue("@EpochIndex", r.EpochIndex);
            cmd.Parameters.AddWithValue("@Time", now);
            cmd.Parameters.AddWithValue("@PrepareQueryPrompt", (object?)r.PrepareQueryPrompt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@QueryInput", (object?)r.QueryInput ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@QueryResult", (object?)r.QueryResult ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EpisodicText", (object?)r.EpisodicText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DatabaseSchema", (object?)r.DatabaseSchema ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ProjectId", r.ProjectId);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            await _writerConn.DisposeAsync();
        }

        // ---------- One-time bootstrap (drop this at the top of SqlAgent.RunAsync) ----------
        private static int _bootstrapGuard = 0;

        /// <summary>
        /// Call once at startup or at the very top of SqlAgent.RunAsync.
        /// Discovers the DB name from your existing connection; uses adminConnString if provided,
        /// otherwise falls back to that same connection string.
        /// Non-fatal: logs and continues on failure.
        /// </summary>
        public static async Task RunBootstrapOnceAsync(
            SqlConnection existingDbConn,
            Action<string>? log = null,
            string? adminConnString = null,
            string storedProcName = DefaultProcName)
        {
            if (existingDbConn == null) throw new ArgumentNullException(nameof(existingDbConn));

            if (Interlocked.Exchange(ref _bootstrapGuard, 1) != 0)
                return; // already ran in this process

            try
            {
                var targetDb = existingDbConn.Database;                 // e.g., "AgenticDb"
                var adminConn = adminConnString ?? existingDbConn.ConnectionString;

                await EnsureObjectsAsync(adminConn, targetDb, storedProcName);

                log?.Invoke($"Episodics bootstrap completed for database '{targetDb}'.");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Episodics bootstrap skipped (non-fatal): {ex.Message}");
            }
        }

        /// <summary>
        /// Creates/refreshes proc, roles, grants/denies (idempotent). Requires a principal with DDL rights.
        /// </summary>
        public static async Task EnsureObjectsAsync(
            string adminConnectionString,
            string databaseName,
            string storedProcName = DefaultProcName)
        {
            if (string.IsNullOrWhiteSpace(adminConnectionString))
                throw new ArgumentNullException(nameof(adminConnectionString));
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentNullException(nameof(databaseName));

            var ddl = $@"
USE [{databaseName}];

-- Roles are harmless on LocalDB; keep if you later move off LocalDB
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{RoleWriter}')
    CREATE ROLE {RoleWriter};
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{RoleLlm}')
    CREATE ROLE {RoleLlm};

-- Stored procedure shell (idempotent)
IF OBJECT_ID(N'{storedProcName}', N'P') IS NULL
    EXEC('CREATE PROCEDURE {storedProcName} AS RETURN 0;');

-- Recreate body to guarantee definition; owner context bypasses table DENY
EXEC(N'
ALTER PROCEDURE {storedProcName}
    @EpisodeId          UNIQUEIDENTIFIER,
    @EpochIndex         INT,
    @Time               DATETIME2,
    @PrepareQueryPrompt NVARCHAR(MAX),
    @QueryInput         NVARCHAR(MAX),
    @QueryResult        NVARCHAR(MAX),
    @EpisodicText       NVARCHAR(MAX),
    @DatabaseSchema     NVARCHAR(MAX),
    @ProjectId          INT
WITH EXECUTE AS OWNER
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Episodics
      (EpisodeId, EpochIndex, [Time], PrepareQueryPrompt, QueryInput, QueryResult,
       EpisodicText, DatabaseSchema, ProjectId)
    VALUES
      (@EpisodeId, @EpochIndex, @Time, @PrepareQueryPrompt, @QueryInput, @QueryResult,
       @EpisodicText, @DatabaseSchema, @ProjectId);
END
');

-- Block ALL direct writes to Episodics (covers LocalDB same-principal case)
DENY INSERT, UPDATE, DELETE, ALTER, REFERENCES ON dbo.Episodics TO PUBLIC;
DENY VIEW DEFINITION ON dbo.Episodics TO PUBLIC;   -- optional
DENY ALTER ON SCHEMA::dbo TO PUBLIC;               -- reduce schema tampering

-- If/when you move off LocalDB, you can scope EXEC/CRUD to roles:
-- GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Episodics TO {RoleWriter}; -- (not needed for owner-signed proc)
-- GRANT EXECUTE ON {storedProcName} TO {RoleWriter};
-- DENY  EXECUTE ON {storedProcName} TO {RoleLlm};
";

            await using var admin = new SqlConnection(adminConnectionString);
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = ddl;
            cmd.CommandType = CommandType.Text;
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();
        }


        public static async Task<EpisodicsLogWriter> CreateForAgentAsync(
            SqlConnection existingDbConn,
            string? episodicsWriterConnectionString,
            Action<string>? log = null,
            string? adminConnString = null,
            string storedProcName = DefaultProcName)
        {
            if (existingDbConn == null) throw new ArgumentNullException(nameof(existingDbConn));

            // Run the idempotent bootstrap once per process
            await RunBootstrapOnceAsync(
                existingDbConn: existingDbConn,
                log: log,
                adminConnString: adminConnString,
                storedProcName: storedProcName
            );

            // Prefer explicit writer principal; otherwise fall back (useful for local/dev).
            var writerConnString = string.IsNullOrWhiteSpace(episodicsWriterConnectionString)
                ? existingDbConn.ConnectionString
                : episodicsWriterConnectionString;

            log?.Invoke($"Episodics writer configured for database '{existingDbConn.Database}' " +
                        (string.IsNullOrWhiteSpace(episodicsWriterConnectionString)
                            ? "(fallback to existing connection)."
                            : "(explicit writer connection)."));

            return new EpisodicsLogWriter(writerConnString, storedProcName);
        }

        public static string BuildDefaultWriterConnectionString(SqlConnection existingConn)
        {
            if (existingConn == null) throw new ArgumentNullException(nameof(existingConn));
            // Reuse the same Windows-auth connection string for the writer channel on LocalDB.
            // Security still holds because table DML is DENIED to PUBLIC and inserts go through the owner-signed proc,
            // AND we block the LLM batch from calling that proc in code.
            return existingConn.ConnectionString;
        }
    }
}
