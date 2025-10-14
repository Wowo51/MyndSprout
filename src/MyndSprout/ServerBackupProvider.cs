// MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.
//
// Auto-backup/restore subsystem for MyndSprout. All operations run EXTERIOR to the database
// (no stored procedures required). Supports full-file backups for LocalDB and BACKUP/RESTORE
// for full SQL Server. Heuristics analyze Episodics health each ~50 epochs; if catastrophic
// degradation is detected the system restores the last good backup and records the event.

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyndSprout
{
    #region Contracts & Models

    /// <summary>
    /// Outcome of a single health check and the action taken (if any).
    /// </summary>
    public sealed class HealthDecision
    {
        public bool IsHealthy { get; init; }
        public bool Restored { get; init; }
        public bool BackedUp { get; init; }
        public string Reason { get; init; } = "";
        public string? BackupPath { get; init; }
        public string? RestoredFrom { get; init; }
        public int Epoch { get; init; }
        public double ScoreDelta { get; init; }
    }

    /// <summary>
    /// Abstraction for backup/restore providers (LocalDB vs full SQL Server).
    /// </summary>
    public interface IBackupProvider
    {
        Task<string> CreateBackupAsync(SqlConnection openConnection, string databaseName, string backupDir, CancellationToken ct);
        Task RestoreBackupAsync(SqlConnection openConnection, string databaseName, string backupPath, CancellationToken ct);
        Task<string> RotateAsync(string latestPath, CancellationToken ct);
        string BackupFileExtension { get; }
    }

    /// <summary>
    /// Settings governing frequency and thresholds.
    /// </summary>
    public sealed class AutoBackupSettings
    {
        /// <summary>Run health + backup decision every N epochs (default 50).</summary>
        public int EpochInterval { get; init; } = 50;
        /// <summary>Where to write backup files.</summary>
        public string BackupDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyndSprout", "Backups");
        /// <summary>Minimum improvement vs prior 50 epochs considered "steady progress" (negative means allow some regression).</summary>
        public double MinProgressDelta { get; init; } = -0.20; // allow 10% wobble
        /// <summary>If score drops by this fraction or more, consider catastrophic and restore.</summary>
        public double CatastrophicDrop { get; init; } = -0.50; // 50% drop
        /// <summary>Hard indicators that force restore even if score looks ok.</summary>
        public int MaxHardErrorsRecent { get; init; } = 25; // e.g., missing table/PK corruption
        /// <summary>Optional project scope for episodics analysis.</summary>
        public int? ProjectId { get; init; } = 1;
        /// <summary>Optional logical name to tag events.</summary>
        public string SystemName { get; init; } = "MyndSprout";
    }

    #endregion

    #region Providers

    /// <summary>
    /// Uses T-SQL BACKUP DATABASE/RESTORE DATABASE. Not available in LocalDB.
    /// </summary>
    public sealed class ServerBackupProvider : IBackupProvider
    {
        public string BackupFileExtension => ".bak";

        public async Task<string> CreateBackupAsync(SqlConnection conn, string databaseName, string backupDir, CancellationToken ct)
        {
            Directory.CreateDirectory(backupDir);
            string file = Path.Combine(backupDir, SafeStamp($"{databaseName}-full-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak"));

            await using var cmd = new SqlCommand($@"
BACKUP DATABASE [{databaseName}] TO DISK = @p WITH INIT, CHECKSUM, COPY_ONLY, FORMAT;", conn);
            cmd.Parameters.AddWithValue("@p", file);
            await cmd.ExecuteNonQueryAsync(ct);
            return file;
        }

        public async Task RestoreBackupAsync(SqlConnection conn, string databaseName, string backupPath, CancellationToken ct)
        {
            // Force single-user restore sequence
            await using (var prep = new SqlCommand($@"
ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", conn))
            {
                await prep.ExecuteNonQueryAsync(ct);
            }

            // Logical names discovery (RESTORE FILELISTONLY)
            string logicalData = "", logicalLog = "";
            await using (var filelist = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @p;", conn))
            {
                filelist.Parameters.AddWithValue("@p", backupPath);
                using var r = await filelist.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    string type = r["Type"].ToString() ?? "D";
                    string name = r["LogicalName"].ToString() ?? "";
                    if (type == "D") logicalData = name;
                    if (type == "L") logicalLog = name;
                }
            }

            await using (var restore = new SqlCommand($@"
RESTORE DATABASE [{databaseName}] FROM DISK = @p WITH REPLACE;", conn))
            {
                restore.Parameters.AddWithValue("@p", backupPath);
                await restore.ExecuteNonQueryAsync(ct);
            }

            await using (var multi = new SqlCommand($@"
ALTER DATABASE [{databaseName}] SET MULTI_USER;", conn))
            {
                await multi.ExecuteNonQueryAsync(ct);
            }
        }

        public Task<string> RotateAsync(string latestPath, CancellationToken ct)
        {
            string prev = Path.ChangeExtension(latestPath, ".prev.bak");
            if (File.Exists(prev)) File.Delete(prev);
            if (File.Exists(latestPath)) File.Move(latestPath, prev);
            return Task.FromResult(prev);
        }

        private static string SafeStamp(string name) => Regex.Replace(name, "[^A-Za-z0-9_.\\-]", "_");
    }

    /// <summary>
    /// LocalDB backup provider using file copies of MDF/LDF. Requires the DB to be detached
    /// momentarily. Use only for (localdb)\\MSSQLLocalDB. Administrator rights not required.
    /// </summary>
    public sealed class LocalDbFileBackupProvider : IBackupProvider
    {
        public string BackupFileExtension => ".zip"; // we zip the pair for atomicity

        public async Task<string> CreateBackupAsync(SqlConnection conn, string databaseName, string backupDir, CancellationToken ct)
        {
            Directory.CreateDirectory(backupDir);

            // Capture app connection string and proactively close the app's DB-bound connection to reduce contention.
            string appConnStr = conn.ConnectionString;
            try { await conn.CloseAsync(); } catch { }

            // Use a separate master connection for exclusivity + detach/attach
            using var master = OpenMasterSibling(appConnStr);

            var (mdf, ldf) = await GetDbFilesAsync(master, databaseName, ct); // query file paths from master
            string zipPath = Path.Combine(backupDir, $"{databaseName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");

            await ForceExclusiveAsync(master, databaseName, ct);
            await ExecAsync(master, "EXEC sp_detach_db @dbname", new() { ["@dbname"] = databaseName }, ct);

            try
            {
                using var zip = new System.IO.Compression.ZipArchive(File.Create(zipPath), System.IO.Compression.ZipArchiveMode.Create);
                zip.CreateEntryFromFile(mdf, Path.GetFileName(mdf));
                if (File.Exists(ldf)) zip.CreateEntryFromFile(ldf, Path.GetFileName(ldf));
            }
            finally
            {
                await ExecAsync(master, $"CREATE DATABASE [{databaseName}] ON (FILENAME = '{mdf.Replace("'", "''")}') FOR ATTACH;", null, ct);
                await ExecAsync(master, $"ALTER DATABASE [{databaseName}] SET MULTI_USER;", null, ct);
            }

            return zipPath;
        }

        public async Task RestoreBackupAsync(SqlConnection conn, string databaseName, string backupPath, CancellationToken ct)
        {
            string appConnStr = conn.ConnectionString;
            try { await conn.CloseAsync(); } catch { }

            using var master = OpenMasterSibling(appConnStr);
            var (mdf, ldf) = await GetDbFilesAsync(master, databaseName, ct);

            await ForceExclusiveAsync(master, databaseName, ct);
            await ExecAsync(master, "EXEC sp_detach_db @dbname", new() { ["@dbname"] = databaseName }, ct);

            try
            {
                string tempDir = Path.Combine(Path.GetDirectoryName(mdf)!, "_restore_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                System.IO.Compression.ZipFile.ExtractToDirectory(backupPath, tempDir, overwriteFiles: true);

                File.Copy(Path.Combine(tempDir, Path.GetFileName(mdf)), mdf, overwrite: true);
                string srcLdf = Path.Combine(tempDir, Path.GetFileName(ldf));
                if (!string.IsNullOrEmpty(ldf) && File.Exists(srcLdf)) File.Copy(srcLdf, ldf, overwrite: true);

                Directory.Delete(tempDir, true);
            }
            finally
            {
                await ExecAsync(master, $"CREATE DATABASE [{databaseName}] ON (FILENAME = '{mdf.Replace("'", "''")}') FOR ATTACH;", null, ct);
                await ExecAsync(master, $"ALTER DATABASE [{databaseName}] SET MULTI_USER;", null, ct);
            }
        }

        public Task<string> RotateAsync(string latestPath, CancellationToken ct)
        {
            string prev = Path.Combine(Path.GetDirectoryName(latestPath)!, Path.GetFileNameWithoutExtension(latestPath) + ".prev.zip");
            if (File.Exists(prev)) File.Delete(prev);
            if (File.Exists(latestPath)) File.Move(latestPath, prev);
            return Task.FromResult(prev);
        }

        private static SqlConnection OpenMasterSibling(string likeConnectionString)
        {
            var b = new SqlConnectionStringBuilder(likeConnectionString) { InitialCatalog = "master" };
            // Avoid pooling to ensure we don't hold accidental references to the target DB during detach
            b.Pooling = false;
            var c = new SqlConnection(b.ConnectionString);
            c.Open();
            return c;
        }

        private static async Task ForceExclusiveAsync(SqlConnection master, string db, CancellationToken ct)
        {
            // Put DB in SINGLE_USER and rollback immediately
            await ExecAsync(master, $"ALTER DATABASE [{db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", null, ct);

            // Brutally kill straggler sessions still pointing at the DB (defensive on LocalDB)
            const string killSql = @"
DECLARE @db sysname = @dbname;

-- Collect all sessions touching @db from multiple DMV sources (deduped)
;WITH s AS (
    SELECT s.session_id
    FROM sys.dm_exec_sessions AS s
    WHERE s.database_id = DB_ID(@db)

    UNION
    SELECT r.session_id
    FROM sys.dm_exec_requests AS r
    WHERE r.database_id = DB_ID(@db)

    UNION
    SELECT spid AS session_id
    FROM sys.sysprocesses
    WHERE dbid = DB_ID(@db)
)
SELECT session_id
INTO #kill_list
FROM (
    SELECT DISTINCT session_id
    FROM s
    WHERE session_id <> @@SPID
) d;

-- Execute KILLs one-by-one (compatible with all versions)
DECLARE @sid int;
DECLARE c CURSOR FAST_FORWARD FOR SELECT session_id FROM #kill_list;
OPEN c;
FETCH NEXT FROM c INTO @sid;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @cmd nvarchar(50) = N'KILL ' + CONVERT(varchar(11), @sid) + N';';
    EXEC(@cmd);
    FETCH NEXT FROM c INTO @sid;
END
CLOSE c;
DEALLOCATE c;

DROP TABLE #kill_list;
";
            await ExecAsync(master, killSql, new() { ["@dbname"] = db }, ct);
        }

        private static async Task<(string mdf, string ldf)> GetDbFilesAsync(SqlConnection conn, string db, CancellationToken ct)
        {
            using var cmd = new SqlCommand(@"
SELECT m.physical_name, m.type_desc
FROM sys.master_files m
JOIN sys.databases d ON d.database_id = m.database_id
WHERE d.name = @db;", conn);
            cmd.Parameters.AddWithValue("@db", db);
            string mdf = "", ldf = "";
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                string path = r.GetString(0);
                string type = r.GetString(1);
                if (type.Contains("ROWS", StringComparison.OrdinalIgnoreCase)) mdf = path;
                if (type.Contains("LOG", StringComparison.OrdinalIgnoreCase)) ldf = path;
            }
            if (string.IsNullOrWhiteSpace(mdf)) throw new InvalidOperationException("Could not locate MDF path for LocalDB.");
            return (mdf, ldf);
        }

        private static async Task ExecAsync(SqlConnection conn, string sql, Dictionary<string, object?>? p, CancellationToken ct)
        {
            using var cmd = new SqlCommand(sql, conn);
            if (p != null)
                foreach (var kv in p) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    #endregion

    #region Health Analyzer

    /// <summary>
    /// Computes a coarse health score for the Episodics stream and detects catastrophic damage.
    /// </summary>
    public sealed class EpisodicsHealthAnalyzer
    {
        public sealed class Window
        {
            public int Count { get; init; }
            public int Errors { get; init; }
            public int NullOrMissing { get; init; }
            public int SchemaRequests { get; init; }
            public double Score { get; init; }
        }

        /// <summary>
        /// Score recent 50 vs previous 50. Heuristics: more successes, fewer <Error>, non-empty EpisodicText,
        /// and some proportion of schema requests early on but not exploding.
        /// </summary>
        public async Task<(Window prev, Window recent, double delta, int hardErrors)> ScoreAsync(SqlConnection conn, int? projectId, CancellationToken ct, int lookBack)
        {
            string where = projectId.HasValue ? "WHERE ProjectId = @pid" : string.Empty;
            string sql = $@"
;WITH E AS (
  SELECT TOP (100)
     EpisodeId, EpochIndex, [Time], QueryResult, EpisodicText
  FROM dbo.Episodics
  {where}
  ORDER BY EpochIndex DESC, [Time] DESC
)
SELECT * FROM E ORDER BY EpochIndex DESC, [Time] DESC;";

            using var cmd = new SqlCommand(sql, conn);
            if (projectId.HasValue) cmd.Parameters.AddWithValue("@pid", projectId.Value);

            var rows = new List<(int epoch, string qr, string epi)>();
            using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                int epoch = r.GetInt32(r.GetOrdinal("EpochIndex"));
                string qr = r["QueryResult"] as string ?? "";
                string epi = r["EpisodicText"] as string ?? "";
                rows.Add((epoch, qr, epi));
            }

            var recent = ScoreWindow(rows.Take(lookBack).ToArray());
            var prev = ScoreWindow(rows.Skip(lookBack).Take(lookBack).ToArray());

            // HARD ERRORS: missing table, PK corruption, severe parser messages in last lookBack
            int hard = rows.Take(lookBack).Count(t =>
                ContainsAny(t.qr, "Invalid object name", "Could not find stored procedure", "PRIMARY KEY", "FOREIGN KEY", "database is in suspect state")
            );

            return (prev, recent, recent.Score - prev.Score, hard);
        }

        private static Window ScoreWindow((int epoch, string qr, string epi)[] arr)
        {
            if (arr.Length == 0) return new Window { Count = 0, Score = 0 };
            int errors = arr.Count(t => t.qr.Contains("<Error>", StringComparison.OrdinalIgnoreCase) || t.epi.Contains("API Error", StringComparison.OrdinalIgnoreCase));
            int blanks = arr.Count(t => string.IsNullOrWhiteSpace(t.epi));
            int schemaReqHints = arr.Count(t => t.qr.Contains("<Columns>", StringComparison.OrdinalIgnoreCase) && t.qr.Contains("<ResultSet", StringComparison.OrdinalIgnoreCase));

            // simple linear combination
            double ok = arr.Length - errors - blanks;
            double score = (ok / arr.Length) - 0.5 * (errors / (double)arr.Length) - 0.2 * (blanks / (double)arr.Length);
            score = Math.Clamp(score, -1.0, 1.0);
            return new Window { Count = arr.Length, Errors = errors, NullOrMissing = blanks, SchemaRequests = schemaReqHints, Score = score };
        }

        private static bool ContainsAny(string s, params string[] needles)
        {
            foreach (var n in needles)
                if (!string.IsNullOrEmpty(n) && s.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
    }

    #endregion

    #region Event Logging (DB)

    public partial class SqlService
    {
        public async Task EnsureHealthEventsTableAsync()
        {
            const string ddl = @"
IF OBJECT_ID('dbo.HealthEvents','U') IS NULL
BEGIN
  CREATE TABLE dbo.HealthEvents(
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    [TimeUtc]     DATETIME2(3) NOT NULL CONSTRAINT DF_HealthEvents_Time DEFAULT SYSUTCDATETIME(),
    SystemName    NVARCHAR(128) NOT NULL,
    DatabaseName  NVARCHAR(128) NOT NULL,
    Epoch         INT NOT NULL,
    EventType     NVARCHAR(32) NOT NULL, -- Backup|Restore|Warning|Error
    Reason        NVARCHAR(MAX) NOT NULL,
    Details       NVARCHAR(MAX) NULL
  );
END";
            await ExecuteNonQueryAsync(ddl);
        }

        public async Task InsertHealthEventAsync(string systemName, string databaseName, int epoch, string eventType, string reason, string? details = null)
        {
            const string sql = @"
INSERT INTO dbo.HealthEvents(SystemName, DatabaseName, Epoch, EventType, Reason, Details)
VALUES(@s,@d,@e,@t,@r,@z);";
            await ExecuteNonQueryAsync(sql, new()
            {
                ["@s"] = systemName,
                ["@d"] = databaseName,
                ["@e"] = epoch,
                ["@t"] = eventType,
                ["@r"] = reason,
                ["@z"] = (object?)details ?? DBNull.Value
            });
        }
    }

    #endregion

    #region Orchestrator

    /// <summary>
    /// Glue class: called by SqlAgent roughly each epoch; executes health checks every N epochs
    /// and decides to restore/backup. Keeps previous backup rotated as *.prev.* to allow double safety.
    /// </summary>
    public sealed class AutoBackupManager
    {
        private readonly SqlStrings _sql;
        private readonly string _databaseName;
        private readonly AutoBackupSettings _settings;
        private readonly EpisodicsHealthAnalyzer _analyzer = new();
        private readonly IBackupProvider _provider;

        public AutoBackupManager(SqlStrings sqlStrings, string databaseName, AutoBackupSettings? settings = null, IBackupProvider? provider = null)
        {
            _sql = sqlStrings ?? throw new ArgumentNullException(nameof(sqlStrings));
            _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            _settings = settings ?? new AutoBackupSettings();

            // decide provider based on DataSource
            string ds = sqlStrings.Database?.DataSource ?? "";
            if (ds.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
                _provider = provider ?? new LocalDbFileBackupProvider();
            else
                _provider = provider ?? new ServerBackupProvider();
        }

        /// <summary>
        /// Call every epoch. Performs work only when epoch % Interval == 0.
        /// </summary>
        public async Task<HealthDecision?> TickAsync(int epoch, CancellationToken ct = default)
        {
            // Only act every N epochs
            if (epoch <= 0 || epoch % _settings.EpochInterval != 0) return null;

            await _sql.EnsureHealthEventsTableAsync();

            // Snapshot current app connection details for safe reconnection after backup/restore
            string appConnStr = _sql.Database?.ConnectionString ?? string.Empty;
            string dbName = _sql.Database?.Database ?? _databaseName;

            // Helper to guarantee our SqlStrings connection is usable post-operation
            async Task EnsureReconnectAsync()
            {
                try
                {
                    if (_sql.Database == null || _sql.Database.State != ConnectionState.Open)
                    {
                        _sql.Database?.Dispose();
                        _sql.Database = new SqlConnection(appConnStr);
                        await _sql.Database.OpenAsync(ct);
                    }
                }
                catch
                {
                    // Fallback to the existing Connect* path if something unusual happened
                    string connectXml = $"<ConnectInput><ConnectionString>{System.Security.SecurityElement.Escape(appConnStr)}</ConnectionString></ConnectInput>";
                    await _sql.ConnectAsyncStr(connectXml);
                }
            }

            // Analyze Episodics health (last 100: previous 50 vs recent 50)
            var (prev, recent, delta, hard) = await _analyzer.ScoreAsync(_sql.Database!, _settings.ProjectId, ct, _settings.EpochInterval);

            bool catastrophic = (delta <= _settings.CatastrophicDrop)
                              || (hard >= _settings.MaxHardErrorsRecent)
                              || recent.Count < 10; // too few recent rows implies potential wipe/corruption

            // === Restore path ===
            if (catastrophic)
            {
                string? last = FindLatestBackupPath(_settings.BackupDirectory, dbName, _provider.BackupFileExtension);
                if (last == null)
                {
                    // No prior backup: record error and snapshot a forensic backup of current (damaged) state
                    await _sql.InsertHealthEventAsync(_settings.SystemName, dbName, epoch, "Error",
                        "Catastrophic damage but no backup found.",
                        $"Delta={delta:F2}, HardErrors={hard}, RecentCount={recent.Count}");

                    string forensic = await _provider.CreateBackupAsync(_sql.Database!, dbName, _settings.BackupDirectory, ct);
                    await EnsureReconnectAsync();
                    await _sql.InsertHealthEventAsync(_settings.SystemName, dbName, epoch, "Backup",
                        "Forensic backup of damaged state.", forensic);

                    return new HealthDecision
                    {
                        IsHealthy = false,
                        Restored = false,
                        BackedUp = true,
                        Reason = "Catastrophic damage; no previous backup.",
                        BackupPath = forensic,
                        Epoch = epoch,
                        ScoreDelta = delta
                    };
                }

                // Restore from the last good backup. Providers handle exclusivity (master connection, SINGLE_USER/DETACH) internally.
                await _provider.RestoreBackupAsync(_sql.Database!, dbName, last, ct);
                await EnsureReconnectAsync();
                await _sql.InsertHealthEventAsync(_settings.SystemName, dbName, epoch, "Restore",
                    "Catastrophic health drop or hard errors — restored last good backup.", last);

                return new HealthDecision
                {
                    IsHealthy = false,
                    Restored = true,
                    BackedUp = false,
                    Reason = "Catastrophic drop or hard errors.",
                    RestoredFrom = last,
                    Epoch = epoch,
                    ScoreDelta = delta
                };
            }

            // === Healthy/steady path: rotate and create a fresh backup ===
            string latest = Path.Combine(_settings.BackupDirectory, $"{dbName}-latest{_provider.BackupFileExtension}");

            if (File.Exists(latest))
            {
                string prevPath = await _provider.RotateAsync(latest, ct);
                await _sql.InsertHealthEventAsync(_settings.SystemName, dbName, epoch, "Backup",
                    "Rotated previous backup.", prevPath);
            }

            string bak = await _provider.CreateBackupAsync(_sql.Database!, dbName, _settings.BackupDirectory, ct);
            await EnsureReconnectAsync();

            // Maintain a stable "latest" copy for convenience
            TryCopy(bak, latest);
            await _sql.InsertHealthEventAsync(_settings.SystemName, dbName, epoch, "Backup",
                "Healthy progress — new backup created.", bak);

            return new HealthDecision
            {
                IsHealthy = true,
                Restored = false,
                BackedUp = true,
                Reason = "Healthy or acceptable progress.",
                BackupPath = bak,
                Epoch = epoch,
                ScoreDelta = delta
            };
        }

        private static string? FindLatestBackupPath(string dir, string dbName, string ext)
        {
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, $"{dbName}-*{ext}");
            if (files.Length == 0) return null;
            return files.OrderByDescending(File.GetCreationTimeUtc).FirstOrDefault();
        }

        private static void TryCopy(string src, string dest)
        {
            try { File.Copy(src, dest, overwrite: true); }
            catch { /* best-effort */ }
        }
    }

    #endregion

    #region SqlAgent integration helper (minimal touch)

    public static class SqlAgentBackupIntegration
    {
        /// <summary>
        /// Create and attach an AutoBackupManager to an agent. Call from your factory after connecting.
        /// </summary>
        public static AutoBackupManager AttachAutoBackup(this SqlAgent agent, string databaseName, AutoBackupSettings? settings = null, IBackupProvider? provider = null)
        {
            var mgr = new AutoBackupManager(agent.Sql, databaseName, settings, provider);
            // Wire into OnEpochEnd with the current epoch if available.
            // SqlAgent presently raises OnEpochEnd without args; we call mgr.TickAsync from RunAsync after each SaveEpisodic.
            return mgr;
        }
    }

    #endregion
}
