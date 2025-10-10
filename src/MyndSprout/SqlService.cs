//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyndSprout
{
    public partial class SqlService
    {
        public SqlConnection Database { get; set; } = null!;

        public static async Task<int> CreateDatabaseAsync(string serverConnectionString, string dbName)
        {
            // Force initial catalog = master to ensure we can create another DB
            var builder = new SqlConnectionStringBuilder(serverConnectionString)
            {
                InitialCatalog = "master"
            };

            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();

            var sql = $@"
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = @dbName)
BEGIN
    EXEC('CREATE DATABASE [' + @dbName + ']');
END";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dbName", dbName);
            return await cmd.ExecuteNonQueryAsync();
        }

        public static async Task DropDatabaseAsync(string serverConnectionString, string dbName)
        {
            var builder = new SqlConnectionStringBuilder(serverConnectionString)
            {
                InitialCatalog = "master"
            };

            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();

            var sql = $@"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = @dbName)
BEGIN
    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{dbName}];
END";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dbName", dbName);
            await cmd.ExecuteNonQueryAsync();
        }

        // Convenience static helper to execute a non-query against a provided connection string.
        // Optional parameters may be provided; typical usage in tests: ExecuteNonQueryAsync(connStr, "CREATE TABLE ...")
        public static async Task<int> ExecuteNonQueryAsync(string connectionString, string sql, Dictionary<string, object?>? parameters = null)
        {
            var builder = new SqlConnectionStringBuilder(connectionString ?? string.Empty);
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql ?? string.Empty, conn);
            if (parameters != null)
            {
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
            }

            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task ConnectAsync(string connectionstring)
        {
            Database = new SqlConnection(connectionstring);
            await Database.OpenAsync();
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            if (Database == null) throw new InvalidOperationException("Not connected.");

            await using var cmd = new SqlCommand(sql, Database);
            if (parameters != null)
            {
                foreach (var (name, value) in parameters)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            if (Database == null) throw new InvalidOperationException("Not connected.");

            await using var cmd = new SqlCommand(sql, Database);
            if (parameters != null)
            {
                foreach (var (name, value) in parameters)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            return rows;
        }

        public async Task<Dictionary<string, object?>> GetSchemaAsync()
        {
            if (Database == null) throw new InvalidOperationException("Not connected.");

            var schema = new Dictionary<string, object?>();

            schema["Tables"] = await ExecuteQueryAsync(@"
SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
ORDER BY TABLE_SCHEMA, TABLE_NAME;");

            schema["Columns"] = await ExecuteQueryAsync(@"
SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, ORDINAL_POSITION, DATA_TYPE,
       CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;");

            schema["PrimaryKeys"] = await ExecuteQueryAsync(@"
SELECT KU.TABLE_SCHEMA, KU.TABLE_NAME, KU.COLUMN_NAME, KU.ORDINAL_POSITION
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS TC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KU
  ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
ORDER BY KU.TABLE_SCHEMA, KU.TABLE_NAME, KU.ORDINAL_POSITION;");

            schema["ForeignKeys"] = await ExecuteQueryAsync(@"
SELECT fk.name AS ForeignKey, sch1.name AS TableSchema, t1.name AS TableName,
       c1.name AS ColumnName, sch2.name AS RefSchema, t2.name AS RefTable, c2.name AS RefColumnName
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables t1 ON fkc.parent_object_id = t1.object_id
JOIN sys.schemas sch1 ON t1.schema_id = sch1.schema_id
JOIN sys.columns c1 ON fkc.parent_object_id = c1.object_id AND fkc.parent_column_id = c1.column_id
JOIN sys.tables t2 ON fkc.referenced_object_id = t2.object_id
JOIN sys.schemas sch2 ON t2.schema_id = sch2.schema_id
JOIN sys.columns c2 ON fkc.referenced_object_id = c2.object_id AND fkc.referenced_column_id = c2.column_id
ORDER BY sch1.name, t1.name, fk.name;");

            schema["Indexes"] = await ExecuteQueryAsync(@"
SELECT s.name AS SchemaName, t.name AS TableName, i.name AS IndexName,
       i.is_unique, i.is_primary_key, c.name AS ColumnName
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.index_columns ic ON ic.object_id = t.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
WHERE i.is_hypothetical = 0 AND i.type_desc <> 'HEAP'
ORDER BY s.name, t.name, i.name;");

            schema["Views"] = await ExecuteQueryAsync(@"
SELECT s.name AS SchemaName, v.name AS ViewName, m.definition
FROM sys.views v
JOIN sys.schemas s ON s.schema_id = v.schema_id
LEFT JOIN sys.sql_modules m ON m.object_id = v.object_id
ORDER BY s.name, v.name;");

            schema["Procedures"] = await ExecuteQueryAsync(@"
SELECT s.name AS SchemaName, p.name AS ProcName, m.definition
FROM sys.procedures p
JOIN sys.schemas s ON s.schema_id = p.schema_id
LEFT JOIN sys.sql_modules m ON m.object_id = p.object_id
ORDER BY s.name, p.name;");

            schema["Functions"] = await ExecuteQueryAsync(@"
SELECT s.name AS SchemaName, o.name AS FunctionName, o.type_desc, m.definition
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules m ON m.object_id = o.object_id
WHERE o.type IN ('FN','IF','TF')
ORDER BY s.name, o.name;");

            return schema;
        }


        // In SqlService (same class where your other DB helpers live)
        public async Task EnsureEpisodicsTableAsync()
        {
            const string ddl = @"
IF OBJECT_ID('dbo.Episodics', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Episodics (
        EpisodeId           UNIQUEIDENTIFIER NOT NULL,
        EpochIndex          INT              NOT NULL,
        [Time]              DATETIME2(3)     NOT NULL CONSTRAINT DF_Episodics_Time DEFAULT SYSUTCDATETIME(),
        PrepareQueryPrompt  NVARCHAR(MAX)    NOT NULL CONSTRAINT DF_Episodics_Prep   DEFAULT (N''),
        QueryInput          NVARCHAR(MAX)    NOT NULL CONSTRAINT DF_Episodics_Input  DEFAULT (N''),
        QueryResult         NVARCHAR(MAX)    NOT NULL CONSTRAINT DF_Episodics_Result DEFAULT (N''),
        EpisodicText        NVARCHAR(MAX)    NOT NULL CONSTRAINT DF_Episodics_Text   DEFAULT (N''),
        DatabaseSchema      NVARCHAR(MAX)    NOT NULL CONSTRAINT DF_Episodics_Schema DEFAULT (N''),
        ProjectId           INT              NOT NULL CONSTRAINT DF_Episodics_Project DEFAULT (1),
        CONSTRAINT PK_Episodics PRIMARY KEY CLUSTERED (EpisodeId, EpochIndex)
    );

    CREATE INDEX IX_Episodics_Time ON dbo.Episodics ([Time] DESC);
    CREATE INDEX IX_Episodics_Project_Time ON dbo.Episodics (ProjectId, [Time] DESC);
END";
            await ExecuteNonQueryAsync(ddl);
        }

        public async Task SaveEpisodicAsync(EpisodicRecord e)
        {
            if (Database == null) throw new InvalidOperationException("Not connected.");

            const string sql = @"
INSERT INTO dbo.Episodics
    (EpisodeId, EpochIndex, [Time], PrepareQueryPrompt, QueryInput, QueryResult, EpisodicText, [DatabaseSchema], ProjectId)
VALUES
    (@EpisodeId, @EpochIndex, @Time, @PrepareQueryPrompt, @QueryInput, @QueryResult, @EpisodicText, @DatabaseSchema, @ProjectId);";

            var p = new Dictionary<string, object?>
            {
                ["@EpisodeId"] = e.EpisodeId,
                ["@EpochIndex"] = e.EpochIndex,
                ["@Time"] = e.Time,
                ["@PrepareQueryPrompt"] = e.PrepareQueryPrompt ?? string.Empty,
                ["@QueryInput"] = e.QueryInput ?? string.Empty,
                ["@QueryResult"] = e.QueryResult ?? string.Empty,
                ["@EpisodicText"] = e.EpisodicText ?? string.Empty,
                ["@DatabaseSchema"] = e.DatabaseSchema ?? string.Empty,
                ["@ProjectId"] = e.ProjectId,
            };

            await ExecuteNonQueryAsync(sql, p!);
        }

    }
}

