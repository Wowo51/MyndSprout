//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// ExportDataXml.cs
// Simplified, safe implementation to ensure project compiles and tests can run.
// Requires: <PackageReference Include="Microsoft.Data.SqlClient" Version="6.*" />
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Text;

namespace SqlTools;

public static class ExportDataXml
{
    public static string Export(SqlConnection connection, bool includeEmptyTables = true)
    {
        if (connection == null) throw new ArgumentNullException(nameof(connection));
        bool openedHere = false;
        try
        {
            if (connection.State != ConnectionState.Open) { connection.Open(); openedHere = true; }

            var sb = new StringBuilder();
            string dbName = connection.Database ?? string.Empty;
            sb.AppendLine($"<database name=\"{EscapeXmlAttr(dbName)}\">");

            // List all user tables
            var tables = new List<(string Schema, string Table)>();
            using (var cmd = new SqlCommand(
                "SELECT s.name, t.name FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id ORDER BY s.name, t.name",
                connection))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read()) tables.Add((r.GetString(0), r.GetString(1)));
            }

            foreach (var (schema, table) in tables)
            {
                string full = $"{EscapeIdent(schema)}.{EscapeIdent(table)}";
                using var cmd = new SqlCommand($"SELECT * FROM {full}", connection);
                using var rdr = cmd.ExecuteReader();

                if (!rdr.HasRows && !includeEmptyTables) continue;

                sb.AppendLine($"  <table name=\"{EscapeXmlAttr(schema)}.{EscapeXmlAttr(table)}\">");

                while (rdr.Read())
                {
                    sb.AppendLine("    <row>");
                    for (int i = 0; i < rdr.FieldCount; i++)
                    {
                        string col = rdr.GetName(i);
                        object val = rdr.GetValue(i);

                        if (val is DBNull)
                        {
                            sb.AppendLine($"      <col name=\"{EscapeXmlAttr(col)}\" isNull=\"true\" />");
                            continue;
                        }

                        // Prefer readable text. Use CDATA for long/complex strings.
                        if (val is string s)
                        {
                            if (NeedsCData(s))
                            {
                                sb.Append("      <col name=\"").Append(EscapeXmlAttr(col)).Append("\"><![CDATA[");
                                sb.Append(s.Replace("]]>", "]]]]><![CDATA[>")); // split "]]>" safely
                                sb.AppendLine("]]></col>");
                            }
                            else
                            {
                                sb.Append("      <col name=\"").Append(EscapeXmlAttr(col)).Append("\">");
                                sb.Append(EscapeXmlText(s));
                                sb.AppendLine("</col>");
                            }
                        }
                        else if (val is DateTime dt)
                        {
                            sb.Append("      <col name=\"").Append(EscapeXmlAttr(col)).Append("\">");
                            sb.Append(dt.ToString("o"));
                            sb.AppendLine("</col>");
                        }
                        else if (val is byte[] bytes)
                        {
                            // Binary → base64 for safety
                            sb.Append("      <col name=\"").Append(EscapeXmlAttr(col)).Append("\" encoding=\"base64\">");
                            sb.Append(Convert.ToBase64String(bytes));
                            sb.AppendLine("</col>");
                        }
                        else
                        {
                            // numbers, bools, etc.
                            sb.Append("      <col name=\"").Append(EscapeXmlAttr(col)).Append("\">");
                            sb.Append(EscapeXmlText(Convert.ToString(val) ?? ""));
                            sb.AppendLine("</col>");
                        }
                    }
                    sb.AppendLine("    </row>");
                }

                sb.AppendLine("  </table>");
            }

            sb.AppendLine("</database>");
            return sb.ToString();
        }
        finally
        {
            if (openedHere && connection.State == ConnectionState.Open) connection.Close();
        }
    }

    private static string EscapeIdent(string ident) => "[" + ident.Replace("]", "]]") + "]";
    private static bool NeedsCData(string s) =>
        s.Length > 256 || s.IndexOfAny(new[] { '<', '&', '"', '\'', '\r', '\n' }) >= 0;

    private static string EscapeXmlAttr(string s) =>
        (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                           .Replace("\"", "&quot;").Replace("'", "&apos;");

    private static string EscapeXmlText(string s) =>
        (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

}
