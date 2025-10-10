//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace SqlContain;

internal static class SqlHelpers
{
    public static Task ExecBatchesAsync(SqlConnection conn, string sql, params SqlParameter[]? p)
    {
        return ExecBatchesAsync(conn, sql, null, p);
    }

    public static async Task ExecBatchesAsync(SqlConnection conn, string sql, HardenerOptions? options, params SqlParameter[]? p)
    {
        ArgumentNullException.ThrowIfNull(conn);
        if (string.IsNullOrWhiteSpace(sql)) return;

        string pattern = @"^\s*GO\s*$";
        RegexOptions ro = RegexOptions.Multiline | RegexOptions.IgnoreCase;
        string[] batches = Regex.Split(sql, pattern, ro);

        if (conn.State != ConnectionState.Open) await conn.OpenAsync();

        // anchored single-statement USE detection
        string anchoredUsePattern = @"^\s*USE\s+\[?([^\]\s;]+)\]?\s*;?$";
        // leading USE detection (start only)
        string leadingUsePattern = @"^\s*USE\s+\[?([^\]\s;]+)\]?\s*;?\s*";
        RegexOptions useRo = RegexOptions.IgnoreCase;

        foreach (string raw in batches)
        {
            string? batch = raw?.Trim();
            if (string.IsNullOrEmpty(batch)) continue;

            // 1) anchored single-statement USE detection (full-match)
            Match anchoredMatch = Regex.Match(batch, anchoredUsePattern, useRo);
            if (anchoredMatch.Success)
            {
                string target = anchoredMatch.Groups[1].Value;

                string originalInitialCatalog;
                string originalInitialCatalogSource;

                if (!string.IsNullOrEmpty(conn.Database))
                {
                    originalInitialCatalog = conn.Database;
                    originalInitialCatalogSource = "Connection.Database";
                }
                else if (options != null && !string.IsNullOrEmpty(options.Database))
                {
                    originalInitialCatalog = options.Database;
                    originalInitialCatalogSource = "Options.Database";
                }
                else
                {
                    SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder(conn.ConnectionString);
                    originalInitialCatalog = csb.InitialCatalog ?? string.Empty;
                    originalInitialCatalogSource = "ConnectionString.InitialCatalog";
                }

                bool disallow = options?.DisallowUse ?? (new HardenerOptions().DisallowUse);

                if (disallow && !string.Equals(target, originalInitialCatalog, StringComparison.OrdinalIgnoreCase))
                {
                    InvalidOperationException ex = new InvalidOperationException("USE of a different database is disallowed by DisallowUse option.");
                    ex.Data["OffendingSql"] = batch ?? string.Empty;
                    ex.Data["TargetDatabase"] = target;
                    ex.Data["OriginalInitialCatalog"] = originalInitialCatalog;
                    ex.Data["OriginalInitialCatalogSource"] = originalInitialCatalogSource;
                    throw ex;
                }

                // allowed and targets same DB: treat as no-op (skip execution)
                continue;
            }

            // 2) leading USE detection (USE at start but batch contains more)
            Match leadingMatch = Regex.Match(batch, leadingUsePattern, useRo);
            if (leadingMatch.Success && leadingMatch.Index == 0)
            {
                string target = leadingMatch.Groups[1].Value;

                string originalInitialCatalog;
                string originalInitialCatalogSource;

                if (!string.IsNullOrEmpty(conn.Database))
                {
                    originalInitialCatalog = conn.Database;
                    originalInitialCatalogSource = "Connection.Database";
                }
                else if (options != null && !string.IsNullOrEmpty(options.Database))
                {
                    originalInitialCatalog = options.Database;
                    originalInitialCatalogSource = "Options.Database";
                }
                else
                {
                    SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder(conn.ConnectionString);
                    originalInitialCatalog = csb.InitialCatalog ?? string.Empty;
                    originalInitialCatalogSource = "ConnectionString.InitialCatalog";
                }

                bool disallow = options?.DisallowUse ?? (new HardenerOptions().DisallowUse);

                if (disallow && !string.Equals(target, originalInitialCatalog, StringComparison.OrdinalIgnoreCase))
                {
                    InvalidOperationException ex = new InvalidOperationException("USE of a different database is disallowed by DisallowUse option.");
                    ex.Data["OffendingSql"] = batch ?? string.Empty;
                    ex.Data["TargetDatabase"] = target;
                    ex.Data["OriginalInitialCatalog"] = originalInitialCatalog;
                    ex.Data["OriginalInitialCatalogSource"] = originalInitialCatalogSource;
                    throw ex;
                }

                // If allowed: DO NOT split the batch or open a new SqlConnection; execute the entire original batch on the existing connection.
                // fall through to execution below
            }

            using SqlCommand cmdMain = conn.CreateCommand();
            cmdMain.CommandType = CommandType.Text;
            cmdMain.CommandTimeout = 60;
            cmdMain.CommandText = batch;

            if (p is { Length: > 0 })
            {
                foreach (SqlParameter param in p)
                {
                    if (param is ICloneable cloneable)
                    {
                        object cloned = cloneable.Clone();
                        SqlParameter clonedParam = (SqlParameter)cloned;
                        cmdMain.Parameters.Add(clonedParam);
                    }
                    else
                    {
                        SqlParameter np = new SqlParameter(param.ParameterName, param.Value ?? DBNull.Value)
                        {
                            Direction = param.Direction,
                            Size = param.Size,
                            Precision = param.Precision,
                            Scale = param.Scale,
                            IsNullable = param.IsNullable
                        };
                        cmdMain.Parameters.Add(np);
                    }
                }
            }

            try
            {
                await cmdMain.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                ex.Data["Sql"] = batch ?? string.Empty;
                throw new InvalidOperationException($"SQL execution failed. SQL:\n{(batch ?? string.Empty)}", ex);
            }
        }
    }

    public static async Task<T> ExecScalarAsync<T>(SqlConnection conn, string sql, params SqlParameter[]? p)
    {
        using SqlCommand cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text, CommandTimeout = 30 };

        if (p is { Length: > 0 })
        {
            foreach (SqlParameter param in p)
            {
                if (param is ICloneable cloneable)
                {
                    object cloned = cloneable.Clone();
                    SqlParameter clonedParam = (SqlParameter)cloned;
                    cmd.Parameters.Add(clonedParam);
                }
                else
                {
                    SqlParameter np = new SqlParameter(param.ParameterName, param.Value ?? DBNull.Value)
                    {
                        Direction = param.Direction,
                        Size = param.Size,
                        Precision = param.Precision,
                        Scale = param.Scale,
                        IsNullable = param.IsNullable
                    };
                    cmd.Parameters.Add(np);
                }
            }
        }

        try
        {
            object? val = await cmd.ExecuteScalarAsync();
            if (val == null || val is DBNull) return default!;
            return (T)Convert.ChangeType(val, typeof(T))!;
        }
        catch (Exception ex)
        {
            ex.Data["Sql"] = sql ?? string.Empty;
            ExceptionDispatchInfo.Capture(ex).Throw();
            return default!;
        }
    }
}
