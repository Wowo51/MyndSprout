//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlTools;
using MyndSprout;

namespace MyndSproutApp.Actions
{
    public sealed class ImportFolderAction
    {
        private readonly Action<string> _log;

        public ImportFolderAction(Action<string> log) => _log = log ?? (_ => { });

        public async Task RunAsync(MainViewModel vm, string folderPath, bool truncateFirst = false)
        {
            if (vm == null) throw new ArgumentNullException(nameof(vm));
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                _log($"ImportFolder: invalid folder '{folderPath}'.");
                return;
            }

            string connStr = ConnectionStringHelper.BuildFromViewModel(vm);

            try
            {
                var csb = new SqlConnectionStringBuilder(connStr);
                string dbName = csb.InitialCatalog;
                if (string.IsNullOrWhiteSpace(dbName))
                {
                    _log("ImportFolder: Could not determine database name from the connection string. Aborting.");
                    return;
                }

                _log($"ImportFolder: Ensuring database '{dbName}' exists...");
                // The full connStr is fine; CreateDatabaseAsync will connect to master anyway.
                await SqlService.CreateDatabaseAsync(connStr, dbName);
                _log($"ImportFolder: Database '{dbName}' is ready.");
            }
            catch (Exception ex)
            {
                _log($"ImportFolder: Failed to ensure the database exists. Error: {ex.Message}");
                return;
            }

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            // === Derive table name from folder name ===
            string baseFolderName = new DirectoryInfo(folderPath).Name;
            string schema = "dbo";
            string table = MakeValidSqlIdentifier(baseFolderName);

            _log($"ImportFolder: scanning '{folderPath}' ...");
            _log($"ImportFolder: target table will be {schema}.{table}");

            // Recursively find files. (All extensions.)
            var filePaths = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories).ToList();
            _log($"ImportFolder: {filePaths.Count} file(s) found.");

            var records = new List<FileImporter.FileRecord>(filePaths.Count);
            foreach (var path in filePaths)
            {
                try
                {
                    string content = File.ReadAllText(path);
                    var fi = new FileInfo(path);

                    // Store a relative path (unix-style separators) for Name
                    string rel = Path.GetRelativePath(folderPath, path).Replace('\\', '/');

                    records.Add(new FileImporter.FileRecord
                    {
                        Name = rel,
                        Time = fi.LastWriteTimeUtc,
                        Content = content
                    });
                }
                catch (Exception ex)
                {
                    _log($"ImportFolder: failed reading '{path}': {ex.Message}");
                }
            }

            if (records.Count == 0)
            {
                _log("ImportFolder: no files to import.");
                return;
            }

            _log($"ImportFolder: importing {records.Count} file(s) into {schema}.{table} ..."
                + (truncateFirst ? " (truncate first)" : string.Empty));

            // NOTE: FileImporter.ImportAsync is expected to create the table if it does not exist.
            // If your implementation requires an explicit create step, call it here before ImportAsync.
            await FileImporter.ImportAsync(conn, records, schema: schema, table: table, truncateFirst: truncateFirst);

            _log("ImportFolder: import completed.");
        }

        /// <summary>
        /// Convert an arbitrary folder name into a safe SQL identifier (<=128 chars),
        /// allowing letters, digits, and underscores. If the first char isn't a letter or underscore,
        /// we prefix with 'T_'. If the result is empty or a reserved word, we also prefix 'T_'.
        /// </summary>
        private static string MakeValidSqlIdentifier(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                raw = "Files";

            // Replace non [A-Za-z0-9_] with underscore
            var chars = raw.Trim().Select(ch =>
                char.IsLetterOrDigit(ch) ? ch : '_').ToArray();

            var s = new string(chars);

            // Collapse multiple underscores
            while (s.Contains("__"))
                s = s.Replace("__", "_");

            // Trim underscores from ends
            s = s.Trim('_');

            // Enforce max identifier length (SQL Server: 128)
            if (s.Length > 128)
                s = s.Substring(0, 128);

            // If empty after cleanup, default
            if (string.IsNullOrEmpty(s))
                s = "Files";

            // If first char not letter or underscore, prefix
            if (!(char.IsLetter(s[0]) || s[0] == '_'))
                s = "T_" + s;

            // Avoid a few common reserved words or generic names that could collide
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TABLE","SELECT","FROM","WHERE","GROUP","ORDER","USER","INDEX","KEY","PRIMARY","FOREIGN",
                "VIEW","PROC","PROCEDURE","FUNCTION","TRIGGER","SCHEMA","DATABASE","FILE","FILES"
            };
            if (reserved.Contains(s))
                s = "T_" + s;

            return s;
        }
    }
}
