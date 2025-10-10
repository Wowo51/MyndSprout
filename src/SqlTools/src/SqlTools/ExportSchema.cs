//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// ExportSchema.cs
// Requires: <PackageReference Include="Microsoft.Data.SqlClient" Version="6.*" />
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SqlTools;

public static class ExportSchema
{
    public static string Export(SqlConnection connection)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        bool openedHere = false;
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
                openedHere = true;
            }

            var sb = new StringBuilder();
            string dbName = connection.Database ?? string.Empty;

            sb.Append("-- Generated from database: ").AppendLine(dbName);
            sb.Append("-- Generated at: ").AppendLine(DateTimeOffset.Now.ToString("o"));
            sb.AppendLine();

            // 1) Schemas (user-defined, skip dbo/sys/guest/INFORMATION_SCHEMA)
            var schemas = GetUserSchemas(connection);
            foreach (var schema in schemas)
            {
                sb.Append("IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'")
                  .Append(EscapeLiteral(schema))
                  .AppendLine("') BEGIN");
                sb.Append("EXEC('CREATE SCHEMA ").Append(EscapeIdent(schema)).AppendLine("');");
                sb.AppendLine("END");
                sb.AppendLine();
            }

            // 2) Tables + columns (incl. computed cols)
            var tableCols = GetTableColumns(connection); // grouped by (schema, table)
            foreach (var grp in tableCols.GroupBy(c => (c.Schema, c.Table)))
            {
                var schema = grp.Key.Schema;
                var table = grp.Key.Table;
                var cols = grp.OrderBy(c => c.Ordinal).ToList();

                AppendCreateTable(sb, schema, table, cols);
            }

            // 3) Primary keys + unique constraints
            var keys = GetKeyConstraints(connection); // PK + UQ
            foreach (var kc in keys)
            {
                AppendKeyConstraint(sb, kc);
            }

            // 4) Foreign keys
            var fks = GetForeignKeys(connection);
            foreach (var fk in fks)
            {
                AppendForeignKey(sb, fk);
            }

            // 5) Indexes (non-constraint)
            var tablesWithClusteredKey = GetTablesWithClusteredKey(connection);
            var indexes = GetIndexes(connection);
            foreach (var ix in indexes)
            {
                bool forceNonclustered = tablesWithClusteredKey.Contains($"{ix.Schema}.{ix.Table}");
                AppendIndex(sb, ix, forceNonclustered);
            }

            // 6) Check constraints
            var checks = GetCheckConstraints(connection);
            foreach (var ck in checks)
            {
                AppendCheckConstraint(sb, ck);
            }

            // 7) Sequences
            var seqs = GetSequences(connection);
            foreach (var s in seqs)
            {
                AppendSequence(sb, s);
            }

            // 8) Triggers (table-scoped T-SQL)
            var triggers = GetTriggers(connection);
            foreach (var tr in triggers)
            {
                AppendModuleIfMissing(sb, tr.Schema, tr.Name, "TR", tr.Definition);
            }

            // 9) Views
            var views = GetViews(connection);
            foreach (var v in views)
            {
                AppendModuleIfMissing(sb, v.Schema, v.Name, "V", v.Definition);
            }

            // 10) Functions (T-SQL: FN/IF/TF)
            var funcs = GetFunctions(connection);
            foreach (var f in funcs)
            {
                AppendModuleIfMissing(sb, f.Schema, f.Name, f.Type, f.Definition);
            }

            // 11) Procedures
            var procs = GetProcedures(connection);
            foreach (var p in procs)
            {
                AppendModuleIfMissing(sb, p.Schema, p.Name, "P", p.Definition);
            }

            return sb.ToString();
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open)
                connection.Close();
        }
    }

    // ---------- Models ----------

    private sealed record ColRow(
        string Schema, string Table, int Ordinal,
        string Column, string TypeName, int MaxLength, int Precision, int Scale,
        bool IsNullable, bool IsIdentity, string? Collation, string? DefaultDef,
        bool IsComputed, string? ComputedDefinition, bool ComputedPersisted
    );

    private sealed record KeyConstraintRow(
        string Schema, string Table, string ConstraintName, string Type, // PK or UQ
        bool IsClustered, IReadOnlyList<(string Column, bool Desc)> Columns
    );

    private sealed record ForeignKeyRow(
        string Schema, string Table, string Name,
        string RefSchema, string RefTable,
        IReadOnlyList<string> Columns, IReadOnlyList<string> RefColumns,
        string DeleteAction, string UpdateAction, bool IsDisabled, bool NotForReplication
    );

    private sealed record IndexRow(
        string Schema, string Table, string Name,
        bool IsUnique, bool IsClustered, string? FilterDefinition,
        IReadOnlyList<(string Column, bool Desc)> KeyColumns,
        IReadOnlyList<string> IncludedColumns
    );

    private sealed record CheckConstraintRow(
        string Schema, string Table, string Name, string Definition, bool IsDisabled, bool NotForReplication
    );

    private sealed record SequenceRow(
        string Schema, string Name, long StartValue, long Increment, long? MinValue, long? MaxValue,
        bool IsCycling, long? CacheSize
    );

    private sealed record ModuleRow(string Schema, string Name, string Type, string Definition);

    // ---------- Queries ----------

    private static List<string> GetUserSchemas(SqlConnection conn)
    {
        const string q = @"
SELECT s.name
FROM sys.schemas s
WHERE s.name NOT IN (N'dbo',N'sys',N'guest',N'INFORMATION_SCHEMA')
ORDER BY s.name";
        var list = new List<string>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    private static List<ColRow> GetTableColumns(SqlConnection conn)
    {
        const string q = @"
SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    c.column_id AS Ordinal,
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_nullable AS IsNullable,
    COLUMNPROPERTY(c.object_id, c.column_id, 'IsIdentity') AS IsIdentity,
    c.collation_name AS CollationName,
    dc.definition AS DefaultDefinition,
    cc.definition AS ComputedDefinition,
    CASE WHEN cc.column_id IS NULL THEN 0 ELSE 1 END AS IsComputed,
    CASE WHEN cc.is_persisted = 1 THEN 1 ELSE 0 END AS ComputedPersisted
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
ORDER BY SchemaName, TableName, Ordinal;";
        var list = new List<ColRow>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            string schema = r["SchemaName"] as string ?? string.Empty;
            string table = r["TableName"] as string ?? string.Empty;
            int ordinal = Convert.ToInt32(r["Ordinal"]);
            string col = r["ColumnName"] as string ?? string.Empty;
            string type = r["TypeName"] as string ?? string.Empty;
            int maxLen = r["MaxLength"] is DBNull ? 0 : Convert.ToInt32(r["MaxLength"]);
            int prec = r["Precision"] is DBNull ? 0 : Convert.ToInt32(r["Precision"]);
            int scale = r["Scale"] is DBNull ? 0 : Convert.ToInt32(r["Scale"]);
            bool isNull = r["IsNullable"] is DBNull ? true : Convert.ToBoolean(r["IsNullable"]);
            bool isIdent = r["IsIdentity"] is DBNull ? false : Convert.ToInt32(r["IsIdentity"]) != 0;
            string? coll = r["CollationName"] as string;
            string? def = r["DefaultDefinition"] as string;
            bool isComp = r["IsComputed"] is DBNull ? false : Convert.ToInt32(r["IsComputed"]) != 0;
            string? compDef = r["ComputedDefinition"] as string;
            bool compPersist = r["ComputedPersisted"] is DBNull ? false : Convert.ToInt32(r["ComputedPersisted"]) != 0;

            list.Add(new ColRow(schema, table, ordinal, col, type, maxLen, prec, scale, isNull, isIdent, coll, def, isComp, compDef, compPersist));
        }
        return list;
    }

    private static List<KeyConstraintRow> GetKeyConstraints(SqlConnection conn)
    {
        const string q = @"
WITH kc AS (
  SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    k.name AS ConstraintName,
    k.type AS KType,            -- 'PK' or 'UQ'
    i.is_unique AS IsUnique,
    i.type_desc AS TypeDesc,
    k.parent_object_id AS ObjectId,
    k.unique_index_id AS IndexId
  FROM sys.key_constraints k
  JOIN sys.tables t ON t.object_id = k.parent_object_id
  JOIN sys.indexes i ON i.object_id = k.parent_object_id AND i.index_id = k.unique_index_id
)
SELECT
  kc.SchemaName, kc.TableName, kc.ConstraintName, kc.KType,
  CASE WHEN kc.TypeDesc LIKE '%CLUSTERED%' THEN 1 ELSE 0 END AS IsClustered,
  c.name AS ColumnName,
  ic.is_descending_key AS IsDesc,
  ic.key_ordinal AS Ord
FROM kc
JOIN sys.index_columns ic ON ic.object_id = kc.ObjectId AND ic.index_id = kc.IndexId AND ic.is_included_column = 0
JOIN sys.columns c ON c.object_id = kc.ObjectId AND c.column_id = ic.column_id
ORDER BY kc.SchemaName, kc.TableName, kc.ConstraintName, Ord;";
        var rows = new List<(string schema, string table, string name, string type, bool clustered, string col, bool desc, int ord)>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            rows.Add((
                r["SchemaName"] as string ?? "",
                r["TableName"] as string ?? "",
                r["ConstraintName"] as string ?? "",
                r["KType"] as string ?? "",
                (r["IsClustered"] is DBNull) ? false : Convert.ToInt32(r["IsClustered"]) != 0,
                r["ColumnName"] as string ?? "",
                (r["IsDesc"] is DBNull) ? false : Convert.ToInt32(r["IsDesc"]) != 0,
                r["Ord"] is DBNull ? 0 : Convert.ToInt32(r["Ord"])
            ));
        }

        var grouped = rows.GroupBy(x => (x.schema, x.table, x.name, x.type, x.clustered))
                          .Select(g => new KeyConstraintRow(
                              g.Key.schema, g.Key.table, g.Key.name, g.Key.type,
                              g.Key.clustered,
                              g.OrderBy(z => z.ord).Select(z => (z.col, z.desc)).ToList()
                          )).ToList();
        return grouped;
    }

    private static List<ForeignKeyRow> GetForeignKeys(SqlConnection conn)
    {
        const string q = @"
SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    fk.name AS FKName,
    SCHEMA_NAME(rt.schema_id) AS RefSchema,
    rt.name AS RefTable,
    fk.delete_referential_action_desc AS DeleteAction,
    fk.update_referential_action_desc AS UpdateAction,
    fk.is_disabled AS IsDisabled,
    fk.is_not_for_replication AS NotForReplication,
    c1.name AS ColumnName,
    c2.name AS RefColumnName,
    fkc.constraint_column_id AS Ord
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns c1 ON c1.object_id = fk.parent_object_id AND c1.column_id = fkc.parent_column_id
JOIN sys.columns c2 ON c2.object_id = fk.referenced_object_id AND c2.column_id = fkc.referenced_column_id
ORDER BY SchemaName, TableName, FKName, Ord;";
        var rows = new List<(string schema, string table, string name, string rs, string rt, string del, string upd, bool dis, bool nfr, string col, string rcol, int ord)>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            rows.Add((
                r["SchemaName"] as string ?? "",
                r["TableName"] as string ?? "",
                r["FKName"] as string ?? "",
                r["RefSchema"] as string ?? "",
                r["RefTable"] as string ?? "",
                r["DeleteAction"] as string ?? "NO_ACTION",
                r["UpdateAction"] as string ?? "NO_ACTION",
                r["IsDisabled"] is DBNull ? false : Convert.ToBoolean(r["IsDisabled"]),
                r["NotForReplication"] is DBNull ? false : Convert.ToBoolean(r["NotForReplication"]),
                r["ColumnName"] as string ?? "",
                r["RefColumnName"] as string ?? "",
                r["Ord"] is DBNull ? 0 : Convert.ToInt32(r["Ord"])
            ));
        }
        return rows.GroupBy(x => (x.schema, x.table, x.name, x.rs, x.rt, x.del, x.upd, x.dis, x.nfr))
                   .Select(g => new ForeignKeyRow(
                       g.Key.schema, g.Key.table, g.Key.name, g.Key.rs, g.Key.rt,
                       g.OrderBy(z => z.ord).Select(z => z.col).ToList(),
                       g.OrderBy(z => z.ord).Select(z => z.rcol).ToList(),
                       g.Key.del, g.Key.upd, g.Key.dis, g.Key.nfr
                   )).ToList();
    }

    private static List<IndexRow> GetIndexes(SqlConnection conn)
    {
        const string q = @"
WITH ix AS (
  SELECT
    SCHEMA_NAME(t.schema_id) AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.index_id,
    i.object_id,
    i.is_unique AS IsUnique,
    i.type_desc AS TypeDesc,
    i.filter_definition AS FilterDefinition
  FROM sys.indexes i
  JOIN sys.tables t ON t.object_id = i.object_id
  WHERE i.is_hypothetical = 0
    AND i.index_id > 0
    AND i.is_primary_key = 0
    AND i.is_unique_constraint = 0
)
SELECT
  ix.SchemaName, ix.TableName, ix.IndexName, ix.IsUnique,
  CASE WHEN ix.TypeDesc LIKE '%CLUSTERED%' THEN 1 ELSE 0 END AS IsClustered,
  ix.FilterDefinition,
  c.name AS ColumnName,
  ic.is_descending_key AS IsDesc,
  ic.key_ordinal AS Ord,
  ic.is_included_column AS IsIncluded
FROM ix
JOIN sys.index_columns ic ON ic.object_id = ix.object_id AND ic.index_id = ix.index_id
JOIN sys.columns c ON c.object_id = ix.object_id AND c.column_id = ic.column_id
ORDER BY SchemaName, TableName, IndexName, Ord;";
        var rows = new List<(string schema, string table, string name, bool uniq, bool clus, string? filter, string col, bool desc, int ord, bool incl)>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            rows.Add((
                r["SchemaName"] as string ?? "",
                r["TableName"] as string ?? "",
                r["IndexName"] as string ?? "",
                r["IsUnique"] is DBNull ? false : Convert.ToBoolean(r["IsUnique"]),
                r["IsClustered"] is DBNull ? false : Convert.ToInt32(r["IsClustered"]) != 0,
                r["FilterDefinition"] as string,
                r["ColumnName"] as string ?? "",
                r["IsDesc"] is DBNull ? false : Convert.ToInt32(r["IsDesc"]) != 0,
                r["Ord"] is DBNull ? 0 : Convert.ToInt32(r["Ord"]),
                r["IsIncluded"] is DBNull ? false : Convert.ToBoolean(r["IsIncluded"])
            ));
        }
        var grouped = rows.GroupBy(x => (x.schema, x.table, x.name, x.uniq, x.clus, x.filter))
                          .Select(g => new IndexRow(
                              g.Key.schema, g.Key.table, g.Key.name, g.Key.uniq, g.Key.clus, g.Key.filter,
                              g.Where(z => !z.incl).OrderBy(z => z.ord).Select(z => (z.col, z.desc)).ToList(),
                              g.Where(z => z.incl).OrderBy(z => z.ord).Select(z => z.col).ToList()
                          )).ToList();
        return grouped;
    }

    private static List<CheckConstraintRow> GetCheckConstraints(SqlConnection conn)
    {
        const string q = @"
SELECT
  SCHEMA_NAME(t.schema_id) AS SchemaName,
  t.name AS TableName,
  cc.name AS CheckName,
  cc.definition AS Definition,
  cc.is_disabled AS IsDisabled,
  cc.is_not_for_replication AS NotForReplication
FROM sys.check_constraints cc
JOIN sys.tables t ON t.object_id = cc.parent_object_id
ORDER BY SchemaName, TableName, CheckName;";
        var list = new List<CheckConstraintRow>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new CheckConstraintRow(
                r["SchemaName"] as string ?? "",
                r["TableName"] as string ?? "",
                r["CheckName"] as string ?? "",
                r["Definition"] as string ?? "",
                r["IsDisabled"] is DBNull ? false : Convert.ToBoolean(r["IsDisabled"]),
                r["NotForReplication"] is DBNull ? false : Convert.ToBoolean(r["NotForReplication"])
            ));
        }
        return list;
    }

    private static List<SequenceRow> GetSequences(SqlConnection conn)
    {
        const string q = @"
SELECT
  SCHEMA_NAME(s.schema_id) AS SchemaName,
  s.name,
  CAST(s.start_value AS BIGINT) AS StartValue,
  CAST(s.increment AS BIGINT) AS Increment,
  CAST(s.minimum_value AS BIGINT) AS MinValue,
  CAST(s.maximum_value AS BIGINT) AS MaxValue,
  s.is_cycling,
  s.cache_size
FROM sys.sequences s
ORDER BY SchemaName, s.name;";
        var list = new List<SequenceRow>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new SequenceRow(
                r["SchemaName"] as string ?? "",
                r["name"] as string ?? "",
                Convert.ToInt64(r["StartValue"]),
                Convert.ToInt64(r["Increment"]),
                r["MinValue"] is DBNull ? null : Convert.ToInt64(r["MinValue"]),
                r["MaxValue"] is DBNull ? null : Convert.ToInt64(r["MaxValue"]),
                r["is_cycling"] is DBNull ? false : Convert.ToBoolean(r["is_cycling"]),
                r["cache_size"] is DBNull ? (long?)null : Convert.ToInt64(r["cache_size"])
            ));
        }
        return list;
    }

    private static List<ModuleRow> GetTriggers(SqlConnection conn)
    {
        const string q = @"
SELECT SCHEMA_NAME(t.schema_id) AS SchemaName, tr.name, sm.definition
FROM sys.triggers tr
JOIN sys.tables t ON t.object_id = tr.parent_id
JOIN sys.sql_modules sm ON sm.object_id = tr.object_id
ORDER BY SchemaName, tr.name;";
        return ReadModules(conn, q, "TR");
    }

    private static List<ModuleRow> GetViews(SqlConnection conn)
    {
        const string q = @"
SELECT SCHEMA_NAME(v.schema_id) AS SchemaName, v.name, sm.definition
FROM sys.views v
JOIN sys.sql_modules sm ON sm.object_id = v.object_id
ORDER BY SchemaName, v.name;";
        return ReadModules(conn, q, "V");
    }

    private static List<ModuleRow> GetFunctions(SqlConnection conn)
    {
        const string q = @"
SELECT SCHEMA_NAME(o.schema_id) AS SchemaName, o.name, o.type, sm.definition
FROM sys.objects o
JOIN sys.sql_modules sm ON sm.object_id = o.object_id
WHERE o.type IN ('FN','IF','TF')   -- scalar, inline TVF, multi-stmt TVF (T-SQL)
ORDER BY SchemaName, o.name;";
        var list = new List<ModuleRow>();
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ModuleRow(
                r["SchemaName"] as string ?? "",
                r["name"] as string ?? "",
                r["type"] as string ?? "FN",
                r["definition"] as string ?? ""
            ));
        }
        return list;
    }

    private static List<ModuleRow> GetProcedures(SqlConnection conn)
    {
        const string q = @"
SELECT SCHEMA_NAME(p.schema_id) AS SchemaName, p.name, sm.definition
FROM sys.procedures p
JOIN sys.sql_modules sm ON sm.object_id = p.object_id
ORDER BY SchemaName, p.name;";
        return ReadModules(conn, q, "P");
    }

    private static List<ModuleRow> ReadModules(SqlConnection conn, string sql, string type)
    {
        var list = new List<ModuleRow>();
        using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ModuleRow(
                r["SchemaName"] as string ?? "",
                r["name"] as string ?? "",
                type,
                r["definition"] as string ?? ""
            ));
        }
        return list;
    }

    // ---------- Emitters ----------

    private static void AppendCreateTable(StringBuilder sb, string schema, string table, List<ColRow> cols)
    {
        if (string.IsNullOrEmpty(schema)) schema = "dbo";

        sb.Append("IF OBJECT_ID(N'").Append(EscapeLiteral(schema + "." + table)).Append("', N'U') IS NULL").AppendLine();
        sb.AppendLine("BEGIN");
        sb.Append("CREATE TABLE ").Append(EscapeIdent(schema)).Append('.').Append(EscapeIdent(table)).AppendLine(" (");

        for (int i = 0; i < cols.Count; i++)
        {
            var c = cols[i];
            sb.Append("    ").Append(EscapeIdent(c.Column)).Append(' ');

            if (c.IsComputed && !string.IsNullOrWhiteSpace(c.ComputedDefinition))
            {
                sb.Append("AS (").Append(c.ComputedDefinition).Append(')');
                if (c.ComputedPersisted) sb.Append(" PERSISTED");
            }
            else
            {
                AppendTypeSpec(sb, c);
                // COLLATE for textual types
                var lower = c.TypeName.ToLowerInvariant();
                if (!string.IsNullOrEmpty(c.Collation) &&
                    (lower.Contains("char") || lower.Contains("text")))
                {
                    sb.Append(" COLLATE ").Append(c.Collation);
                }
                if (c.IsIdentity) sb.Append(" IDENTITY(1,1)");
                sb.Append(c.IsNullable ? " NULL" : " NOT NULL");
                if (!string.IsNullOrEmpty(c.DefaultDef))
                    sb.Append(" DEFAULT ").Append(c.DefaultDef);
            }

            if (i < cols.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }

        sb.AppendLine(");");
        sb.AppendLine("END");
        sb.AppendLine();
    }

    private static void AppendTypeSpec(StringBuilder sb, ColRow c)
    {
        var t = c.TypeName.ToLowerInvariant();
        if (t is "nvarchar" or "nchar")
        {
            if (c.MaxLength == -1) sb.Append(c.TypeName).Append("(MAX)");
            else sb.Append(c.TypeName).Append('(').Append((c.MaxLength / 2).ToString()).Append(')');
        }
        else if (t is "varchar" or "char" or "varbinary" or "binary")
        {
            if (c.MaxLength == -1) sb.Append(c.TypeName).Append("(MAX)");
            else sb.Append(c.TypeName).Append('(').Append(c.MaxLength.ToString()).Append(')');
        }
        else if (t is "decimal" or "numeric")
        {
            sb.Append(c.TypeName).Append('(').Append(c.Precision).Append(',').Append(c.Scale).Append(')');
        }
        else if (t is "datetime2" or "time" or "datetimeoffset")
        {
            if (c.Scale >= 0) sb.Append(c.TypeName).Append('(').Append(c.Scale).Append(')');
            else sb.Append(c.TypeName);
        }
        else
        {
            sb.Append(c.TypeName);
        }
    }

    private static void AppendKeyConstraint(StringBuilder sb, KeyConstraintRow kc)
    {
        string typeText = kc.Type == "PK" ? "PRIMARY KEY" : "UNIQUE";
        string clustering = kc.IsClustered ? " CLUSTERED" : " NONCLUSTERED";

        sb.Append("IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'")
          .Append(EscapeLiteral(kc.ConstraintName))
          .Append("' AND parent_object_id = OBJECT_ID(N'")
          .Append(EscapeLiteral(kc.Schema + "." + kc.Table))
          .AppendLine("', 'U'))");
        sb.AppendLine("BEGIN");
        sb.Append("ALTER TABLE ").Append(EscapeIdent(kc.Schema)).Append('.').Append(EscapeIdent(kc.Table))
          .Append(" ADD CONSTRAINT ").Append(EscapeIdent(kc.ConstraintName))
          .Append(' ').Append(typeText).Append(clustering).Append(" (");

        for (int i = 0; i < kc.Columns.Count; i++)
        {
            var (col, desc) = kc.Columns[i];
            sb.Append(EscapeIdent(col)).Append(desc ? " DESC" : " ASC");
            if (i < kc.Columns.Count - 1) sb.Append(", ");
        }
        sb.AppendLine(");");
        sb.AppendLine("END");
        sb.AppendLine();
    }

    private static void AppendForeignKey(StringBuilder sb, ForeignKeyRow fk)
    {
        sb.Append("IF OBJECT_ID(N'").Append(EscapeLiteral(fk.Schema + "." + fk.Name)).Append("', N'F') IS NULL").AppendLine();
        sb.AppendLine("BEGIN");
        sb.Append("ALTER TABLE ").Append(EscapeIdent(fk.Schema)).Append('.').Append(EscapeIdent(fk.Table))
          .Append(" ADD CONSTRAINT ").Append(EscapeIdent(fk.Name))
          .Append(" FOREIGN KEY (");
        for (int i = 0; i < fk.Columns.Count; i++)
        {
            sb.Append(EscapeIdent(fk.Columns[i]));
            if (i < fk.Columns.Count - 1) sb.Append(", ");
        }
        sb.Append(") REFERENCES ").Append(EscapeIdent(fk.RefSchema)).Append('.').Append(EscapeIdent(fk.RefTable)).Append(" (");
        for (int i = 0; i < fk.RefColumns.Count; i++)
        {
            sb.Append(EscapeIdent(fk.RefColumns[i]));
            if (i < fk.RefColumns.Count - 1) sb.Append(", ");
        }
        sb.Append(')');

        if (!string.Equals(fk.DeleteAction, "NO_ACTION", StringComparison.OrdinalIgnoreCase))
            sb.Append(" ON DELETE ").Append(fk.DeleteAction.Replace('_', ' '));
        if (!string.Equals(fk.UpdateAction, "NO_ACTION", StringComparison.OrdinalIgnoreCase))
            sb.Append(" ON UPDATE ").Append(fk.UpdateAction.Replace('_', ' '));
        if (fk.NotForReplication) sb.Append(" NOT FOR REPLICATION");

        sb.AppendLine(";");
        if (fk.IsDisabled)
        {
            sb.Append("ALTER TABLE ").Append(EscapeIdent(fk.Schema)).Append('.').Append(EscapeIdent(fk.Table))
              .Append(" NOCHECK CONSTRAINT ").Append(EscapeIdent(fk.Name)).AppendLine(";");
        }
        sb.AppendLine("END");
        sb.AppendLine();
    }

    private static void AppendIndex(StringBuilder sb, IndexRow ix, bool forceNonclustered = false)
    {
        bool isClustered = ix.IsClustered && !forceNonclustered;

        sb.Append("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'")
          .Append(EscapeLiteral(ix.Name))
          .Append("' AND object_id = OBJECT_ID(N'")
          .Append(EscapeLiteral(ix.Schema + "." + ix.Table))
          .AppendLine("', 'U'))");
        sb.AppendLine("BEGIN");
        sb.Append("CREATE");
        if (ix.IsUnique) sb.Append(" UNIQUE");
        sb.Append(isClustered ? " CLUSTERED" : " NONCLUSTERED");
        sb.Append(" INDEX ").Append(EscapeIdent(ix.Name))
          .Append(" ON ").Append(EscapeIdent(ix.Schema)).Append('.').Append(EscapeIdent(ix.Table)).Append(" (");

        for (int i = 0; i < ix.KeyColumns.Count; i++)
        {
            var (col, desc) = ix.KeyColumns[i];
            sb.Append(EscapeIdent(col)).Append(desc ? " DESC" : " ASC");
            if (i < ix.KeyColumns.Count - 1) sb.Append(", ");
        }
        sb.Append(')');

        if (ix.IncludedColumns.Count > 0)
        {
            sb.Append(" INCLUDE (");
            for (int i = 0; i < ix.IncludedColumns.Count; i++)
            {
                sb.Append(EscapeIdent(ix.IncludedColumns[i]));
                if (i < ix.IncludedColumns.Count - 1) sb.Append(", ");
            }
            sb.Append(')');
        }

        if (!string.IsNullOrWhiteSpace(ix.FilterDefinition))
        {
            sb.Append(" WHERE ").Append(ix.FilterDefinition);
        }

        sb.AppendLine(";");
        sb.AppendLine("END");
        sb.AppendLine();
    }


    private static void AppendCheckConstraint(StringBuilder sb, CheckConstraintRow ck)
    {
        sb.Append("IF OBJECT_ID(N'").Append(EscapeLiteral(ck.Schema + "." + ck.Name)).Append("', N'C') IS NULL").AppendLine();
        sb.AppendLine("BEGIN");
        sb.Append("ALTER TABLE ").Append(EscapeIdent(ck.Schema)).Append('.').Append(EscapeIdent(ck.Table))
          .Append(" WITH NOCHECK ADD CONSTRAINT ").Append(EscapeIdent(ck.Name))
          .Append(" CHECK ").Append(ck.Definition).AppendLine(";");
        if (!ck.IsDisabled)
        {
            sb.Append("ALTER TABLE ").Append(EscapeIdent(ck.Schema)).Append('.').Append(EscapeIdent(ck.Table))
              .Append(" CHECK CONSTRAINT ").Append(EscapeIdent(ck.Name)).AppendLine(";");
        }
        else
        {
            sb.Append("ALTER TABLE ").Append(EscapeIdent(ck.Schema)).Append('.').Append(EscapeIdent(ck.Table))
              .Append(" NOCHECK CONSTRAINT ").Append(EscapeIdent(ck.Name)).AppendLine(";");
        }
        if (ck.NotForReplication)
        {
            sb.Append("-- NOTE: NOT FOR REPLICATION is set on ").Append(ck.Name).AppendLine();
        }
        sb.AppendLine("END");
        sb.AppendLine();
    }

    private static void AppendSequence(StringBuilder sb, SequenceRow s)
    {
        sb.Append("IF OBJECT_ID(N'").Append(EscapeLiteral(s.Schema + "." + s.Name)).Append("', N'SO') IS NULL").AppendLine();
        sb.AppendLine("BEGIN");
        sb.Append("CREATE SEQUENCE ").Append(EscapeIdent(s.Schema)).Append('.').Append(EscapeIdent(s.Name))
          .Append(" AS BIGINT START WITH ").Append(s.StartValue)
          .Append(" INCREMENT BY ").Append(s.Increment);

        if (s.MinValue.HasValue) sb.Append(" MINVALUE ").Append(s.MinValue.Value);
        if (s.MaxValue.HasValue) sb.Append(" MAXVALUE ").Append(s.MaxValue.Value);
        sb.Append(s.IsCycling ? " CYCLE" : " NO CYCLE");
        if (s.CacheSize.HasValue) sb.Append(" CACHE ").Append(s.CacheSize.Value);

        sb.AppendLine(";");
        sb.AppendLine("END");
        sb.AppendLine();
    }

    private static void AppendModuleIfMissing(StringBuilder sb, string schema, string name, string typeCode, string definition)
    {
        sb.Append("IF OBJECT_ID(N'").Append(EscapeLiteral(schema + "." + name)).Append("', N'")
          .Append(typeCode).AppendLine("') IS NULL");
        sb.AppendLine("BEGIN");
        // definition already begins with CREATE VIEW/FUNCTION/PROC/TRIGGER
        var def = definition?.Replace("''", "''''") ?? string.Empty; // minimal escaping in case of odd strings in body
        sb.Append("EXEC(N'").Append(def.Replace("'", "''")).AppendLine("');");
        sb.AppendLine("END");
        sb.AppendLine();
    }

    // ---------- Helpers ----------

    private static string EscapeIdent(string ident)
    {
        if (ident == null) throw new ArgumentNullException(nameof(ident));
        return "[" + ident.Replace("]", "]]") + "]";
    }

    private static string EscapeLiteral(string s)
    {
        if (s == null) return string.Empty;
        return s.Replace("'", "''");
    }

    private static HashSet<string> GetTablesWithClusteredKey(SqlConnection conn)
    {
        const string q = @"
SELECT DISTINCT SCHEMA_NAME(t.schema_id) AS SchemaName, t.name AS TableName
FROM sys.key_constraints k
JOIN sys.tables t ON t.object_id = k.parent_object_id
JOIN sys.indexes i ON i.object_id = k.parent_object_id AND i.index_id = k.unique_index_id
WHERE i.type_desc LIKE '%CLUSTERED%';";

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = new SqlCommand(q, conn) { CommandType = CommandType.Text, CommandTimeout = 0 };
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var schema = r.GetString(0);
            var table = r.GetString(1);
            set.Add($"{schema}.{table}");
        }
        return set;
    }

}
