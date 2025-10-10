//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// FileImporter.cs
// Requires: <PackageReference Include="Microsoft.Data.SqlClient" Version="6.*" />
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlTools;

public static class FileImporter
{
    /// <summary>
    /// Simple DTO for file content.
    /// </summary>
    public sealed class FileRecord
    {
        public string Name { get; set; } = string.Empty;   // e.g., "notes/a.txt"
        public DateTime Time { get; set; }                 // last modified or captured time
        public string Content { get; set; } = string.Empty; // text content
    }

    /// <summary>
    /// Creates (if missing) a table dbo.Files with columns:
    ///   Id INT IDENTITY PK, Name NVARCHAR(512), Time DATETIME2(3), Content NVARCHAR(MAX).
    /// Also creates a nonclustered index on (Name, Time).
    /// If the table exists, idempotently ensures columns and index exist and have required sizes.
    /// </summary>
    public static async Task EnsureTableAsync(SqlConnection connection, string schema = "dbo", string table = "Files")
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        var openedHere = false;

        try
        {
            if (connection.State != ConnectionState.Open) { await connection.OpenAsync(); openedHere = true; }

            string fullIdent = $"{EscapeIdent(schema)}.{EscapeIdent(table)}";
            // Check existence
            using (SqlCommand existCmd = new SqlCommand("SELECT OBJECT_ID(@fullname, 'U')", connection) { CommandType = CommandType.Text, CommandTimeout = 0 })
            {
                existCmd.Parameters.AddWithValue("@fullname", $"{schema}.{table}");
                object? objId = await existCmd.ExecuteScalarAsync();
                if (objId == null || objId is DBNull)
                {
                    // create table as before
                    var ddl = $@"
CREATE TABLE {fullIdent}
(
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    [Name]  NVARCHAR(512) NOT NULL,
    [Time]  DATETIME2(3)  NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL
);
CREATE INDEX IX_{table}_Name_Time ON {fullIdent}([Name], [Time]);
";
                    using SqlCommand createCmd = new SqlCommand(ddl, connection) { CommandType = CommandType.Text, CommandTimeout = 0 };
                    await createCmd.ExecuteNonQueryAsync();
                    return;
                }
            }

            // Table exists: enumerate columns
            var existingColumns = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand colsCmd = new SqlCommand(
                "SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;",
                connection) { CommandType = CommandType.Text, CommandTimeout = 0 })
            {
                colsCmd.Parameters.AddWithValue("@schema", schema);
                colsCmd.Parameters.AddWithValue("@table", table);
                using SqlDataReader reader = await colsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string col = reader["COLUMN_NAME"] as string ?? string.Empty;
                    object lenObj = reader["CHARACTER_MAXIMUM_LENGTH"];
                    int? len = lenObj is DBNull ? null : Convert.ToInt32(lenObj);
                    existingColumns[col] = len;
                }
            }

            // Add Time if missing
            if (!existingColumns.ContainsKey("Time"))
            {
                string addTime = $"ALTER TABLE {fullIdent} ADD [Time] DATETIME2(3) NOT NULL CONSTRAINT DF_{table}_Time DEFAULT SYSUTCDATETIME();";
                using SqlCommand addCmd = new SqlCommand(addTime, connection) { CommandType = CommandType.Text, CommandTimeout = 0 };
                await addCmd.ExecuteNonQueryAsync();
            }

            // Add Content if missing
            if (!existingColumns.ContainsKey("Content"))
            {
                string addContent = $"ALTER TABLE {fullIdent} ADD [Content] NVARCHAR(MAX) NOT NULL CONSTRAINT DF_{table}_Content DEFAULT N'';";
                using SqlCommand addCmd = new SqlCommand(addContent, connection) { CommandType = CommandType.Text, CommandTimeout = 0 };
                await addCmd.ExecuteNonQueryAsync();
            }

            // If Name exists and is too short, alter it
            if (existingColumns.TryGetValue("Name", out int? charMax) && charMax.HasValue && charMax.Value < 512)
            {
                string alterName = $"ALTER TABLE {fullIdent} ALTER COLUMN [Name] NVARCHAR(512) NOT NULL;";
                using SqlCommand alterCmd = new SqlCommand(alterName, connection) { CommandType = CommandType.Text, CommandTimeout = 0 };
                await alterCmd.ExecuteNonQueryAsync();
            }

            // Ensure index exists
            string indexName = $"IX_{table}_Name_Time";
            using (SqlCommand idxCheck = new SqlCommand(
                "SELECT COUNT(*) FROM sys.indexes WHERE name = @iname AND object_id = OBJECT_ID(@fullname, 'U');",
                connection) { CommandType = CommandType.Text, CommandTimeout = 0 })
            {
                idxCheck.Parameters.AddWithValue("@iname", indexName);
                idxCheck.Parameters.AddWithValue("@fullname", $"{schema}.{table}");
                object? idxCountObj = await idxCheck.ExecuteScalarAsync();
                int idxCount = idxCountObj is DBNull ? 0 : Convert.ToInt32(idxCountObj);
                if (idxCount == 0)
                {
                    string createIndex = $"CREATE INDEX {indexName} ON {fullIdent}([Name], [Time]);";
                    using SqlCommand createIdx = new SqlCommand(createIndex, connection) { CommandType = CommandType.Text, CommandTimeout = 0 };
                    await createIdx.ExecuteNonQueryAsync();
                }
            }
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open) connection.Close();
        }
    }

    /// <summary>
    /// Imports the provided FileRecord items into dbo.Files using SqlBulkCopy (fast, single round-trip).
    /// Set truncateFirst = true to clear the table before import.
    /// </summary>
    public static async Task ImportAsync(SqlConnection connection, IEnumerable<FileRecord> files,
                                         string schema = "dbo", string table = "Files", bool truncateFirst = false)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        if (files == null) throw new ArgumentNullException(nameof(files));

        var openedHere = false;
        try
        {
            if (connection.State != ConnectionState.Open) { await connection.OpenAsync(); openedHere = true; }

            // Ensure table exists and has required columns
            await EnsureTableAsync(connection, schema, table);

            var full = $"{EscapeIdent(schema)}.{EscapeIdent(table)}";

            if (truncateFirst)
            {
                using var trunc = new SqlCommand($"TRUNCATE TABLE {full};", connection)
                { CommandType = CommandType.Text, CommandTimeout = 0 };
                await trunc.ExecuteNonQueryAsync();
            }

            // Build a DataTable matching destination columns (omit identity Id)
            var dt = new DataTable();
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Time", typeof(DateTime));
            dt.Columns.Add("Content", typeof(string));

            foreach (var f in files)
            {
                var name = f.Name ?? string.Empty;
                var content = f.Content ?? string.Empty;
                dt.Rows.Add(name, f.Time, content);
            }

            // Build source name->index map
            DataTable dataTable = dt;
            var sourceNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                sourceNameToIndex[dataTable.Columns[i].ColumnName] = i;
            }

            // Query destination columns
            var destinationCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand colsCmd = new SqlCommand(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table;",
                connection) { CommandType = CommandType.Text, CommandTimeout = 0 })
            {
                colsCmd.Parameters.AddWithValue("@schema", schema);
                colsCmd.Parameters.AddWithValue("@table", table);
                using SqlDataReader reader = await colsCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string col = reader["COLUMN_NAME"] as string ?? string.Empty;
                    destinationCols.Add(col);
                }
            }

            string[] requiredDestinationColumns = new[] { "Name", "Time", "Content" };
            IEnumerable<string> missingDestinationColumns = requiredDestinationColumns.Except(destinationCols, StringComparer.OrdinalIgnoreCase);
            if (missingDestinationColumns.Any())
            {
                throw new InvalidOperationException($"Missing destination columns required for bulk copy: {string.Join(", ", missingDestinationColumns)}. Available source columns: {string.Join(", ", sourceNameToIndex.Keys)}.");
            }

            using var bulk = new SqlBulkCopy(connection)
            {
                DestinationTableName = full,
                BulkCopyTimeout = 0
            };

            // Map only the intersection (case-insensitive) of source and destination columns
            foreach (string destCol in requiredDestinationColumns)
            {
                if (destinationCols.Contains(destCol) && sourceNameToIndex.TryGetValue(destCol, out int srcIdx))
                {
                    bulk.ColumnMappings.Add(dataTable.Columns[srcIdx].ColumnName, destCol);
                }
            }

            await bulk.WriteToServerAsync(dt);
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open) connection.Close();
        }
    }

    // --- helpers ---
    private static string EscapeIdent(string ident)
    {
        if (string.IsNullOrWhiteSpace(ident)) throw new ArgumentException("Invalid identifier.", nameof(ident));
        return $"[{ident.Replace("]", "]]")}]";
    }
}
