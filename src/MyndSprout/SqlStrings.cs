//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
// Copyright Warren Harding 2025.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;

namespace MyndSprout
{
    /// <summary>
    /// String-in / string-out facade over SqlService.
    /// Every method accepts XML input and returns XML output.
    /// </summary>
    public sealed class SqlStrings : SqlService
    {
        // --------------------- Input DTOs (XML-serializable) ---------------------

        public sealed class CreateDatabaseInput
        {
            public string ServerConnectionString { get; set; } = string.Empty;
            public string DbName { get; set; } = string.Empty;
        }

        public sealed class DropDatabaseInput
        {
            public string ServerConnectionString { get; set; } = string.Empty;
            public string DbName { get; set; } = string.Empty;
        }

        public sealed class ConnectInput
        {
            public string ConnectionString { get; set; } = string.Empty;
        }

        public enum SqlExecutionType
        {
            Query,
            NonQuery,
            Error
        }

        // Extend SqlTextRequest with an ExecutionType property
        public sealed class SqlTextRequest
        {
            public string Sql { get; set; } = string.Empty;
            public List<NameValue>? Parameters { get; set; }

            public SqlExecutionType ExecutionType { get; set; } = SqlExecutionType.Query;
        }

        public sealed class NameValue
        {
            public string Name { get; set; } = string.Empty;
            public string? Value { get; set; }
            public bool IsNull { get; set; } = false; // if true, pass DBNull.Value
        }

        // --------------------- Public string-in / string-out methods ---------------------

        /// <summary>
        /// Wrapper for static SqlService.CreateDatabaseAsync.
        /// Input: &lt;CreateDatabaseInput&gt;&lt;ServerConnectionString&gt;...&lt;/ServerConnectionString&gt;&lt;DbName&gt;...&lt;/DbName&gt;&lt;/CreateDatabaseInput&gt;
        /// </summary>
        public async Task<string> CreateDatabaseAsyncStr(string inXml)
        {
            var req = Common.FromXml<CreateDatabaseInput>(inXml);
            if (req is null) return InvalidInputXml();

            try
            {
                int rc = await SqlService.CreateDatabaseAsync(req.ServerConnectionString, req.DbName);
                return MakeOkXml(xw =>
                {
                    xw.WriteElementString("RecordsAffected", rc.ToString());
                });
            }
            catch (Exception ex)
            {
                return MakeErrorXml(ex);
            }
        }

        /// <summary>
        /// Wrapper for static SqlService.DropDatabaseAsync.
        /// Input: &lt;DropDatabaseInput&gt;...&lt;/DropDatabaseInput&gt;
        /// </summary>
        public async Task<string> DropDatabaseAsyncStr(string inXml)
        {
            var req = Common.FromXml<DropDatabaseInput>(inXml);
            if (req is null) return InvalidInputXml();

            try
            {
                await SqlService.DropDatabaseAsync(req.ServerConnectionString, req.DbName);
                return MakeOkXml();
            }
            catch (Exception ex)
            {
                return MakeErrorXml(ex);
            }
        }

        /// <summary>
        /// Wrapper for ConnectAsync.
        /// Input: &lt;ConnectInput&gt;&lt;ConnectionString&gt;...&lt;/ConnectionString&gt;&lt;/ConnectInput&gt;
        /// </summary>
        public async Task<string> ConnectAsyncStr(string inXml)
        {
            var req = Common.FromXml<ConnectInput>(inXml);
            if (req is null) return InvalidInputXml();

            try
            {
                await base.ConnectAsync(req.ConnectionString);
                return MakeOkXml(xw =>
                {
                    if (Database != null)
                    {
                        xw.WriteElementString("Database", Database.Database ?? "");
                        xw.WriteElementString("DataSource", Database.DataSource ?? "");
                        xw.WriteElementString("State", Database.State.ToString());
                    }
                });
            }
            catch (Exception ex)
            {
                return MakeErrorXml(ex);
            }
        }

        /// <summary>
        /// Accepts a SqlTextRequest with ExecutionType and dispatches
        /// to ExecuteQueryAsync or ExecuteNonQueryAsync automatically.
        /// Returns the result as XML.
        /// </summary>
        public async Task<string> ExecuteAsync(SqlTextRequest req)
        {
            if (req is null) return "<Error>Null request</Error>";

            if (string.IsNullOrWhiteSpace(req.Sql))
                return MakeErrorXml(new InvalidOperationException("CommandText is empty."));

            try
            {
                var dict = ToParameterDictionary(req.Parameters);

                if (req.ExecutionType == SqlExecutionType.NonQuery)
                {
                    int rc = await base.ExecuteNonQueryAsync(req.Sql, dict);
                    return MakeOkXml(xw =>
                    {
                        xw.WriteElementString("RecordsAffected", rc.ToString());
                    });
                }
                else // Query
                {
                    var rows = await base.ExecuteQueryAsync(req.Sql, dict);
                    return MakeOkXml(xw =>
                    {
                        xw.WriteStartElement("Rows");
                        foreach (var row in rows)
                        {
                            xw.WriteStartElement("Row");
                            foreach (var kvp in row)
                            {
                                xw.WriteStartElement("Col");
                                xw.WriteAttributeString("name", kvp.Key);
                                WriteValueContent(xw, kvp.Value);
                                xw.WriteEndElement();
                            }
                            xw.WriteEndElement(); // Row
                        }
                        xw.WriteEndElement(); // Rows
                    });
                }
            }
            catch (Exception ex)
            {
                return MakeErrorXml(ex);
            }
        }

        /// <summary>
        /// String-in version that deserializes Xml into SqlTextRequest and calls ExecuteAsync.
        /// </summary>
        public async Task<string> ExecuteAsyncStr(string inXml)
        {
            var req = Common.FromXml<SqlTextRequest>(inXml);
            if (req is null)
                return "<Result success=\"false\"><Error><Type>Input</Type><Message>Invalid input XML</Message></Error></Result>";

            return await ExecuteAsync(req);
        }

        /// <summary>
        /// Wrapper for GetSchemaAsync that returns a structured XML of all sections.
        /// </summary>
        public async Task<string> GetSchemaAsyncStr(string _ = "<Empty/>")
        {
            try
            {
                var schema = await base.GetSchemaAsync();

                return MakeOkXml(xw =>
                {
                    foreach (var section in schema)
                    {
                        xw.WriteStartElement(section.Key);
                        if (section.Value is List<Dictionary<string, object?>> list)
                        {
                            foreach (var row in list)
                            {
                                xw.WriteStartElement("Row");
                                foreach (var kv in row)
                                {
                                    xw.WriteStartElement("Col");
                                    xw.WriteAttributeString("name", kv.Key);
                                    WriteValueContent(xw, kv.Value);
                                    xw.WriteEndElement(); // Col
                                }
                                xw.WriteEndElement(); // Row
                            }
                        }
                        else
                        {
                            // Fallback serialization
                            xw.WriteElementString("Value", section.Value?.ToString() ?? "");
                        }
                        xw.WriteEndElement(); // section
                    }
                });
            }
            catch (Exception ex)
            {
                return MakeErrorXml(ex);
            }
        }

        /// <summary>
        /// Convenience alias for the existing base.ExecuteToXmlAsync(string).
        /// Input must be a &lt;SqlXmlRequest&gt;...&lt;/SqlXmlRequest&gt; (as defined in SqlToXml.cs).
        /// </summary>
        public Task<string> ExecuteToXmlAsyncStr(string inXml) => base.ExecuteToXmlAsync(inXml);

        // --------------------- Helpers ---------------------

        private static Dictionary<string, object> ToParameterDictionary(List<NameValue>? parameters)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (parameters == null) return dict;

            foreach (var p in parameters)
            {
                if (string.IsNullOrWhiteSpace(p.Name)) continue;
                dict[p.Name] = p.IsNull ? DBNull.Value : (object?)p.Value ?? DBNull.Value;
            }
            return dict;
        }

        private static string InvalidInputXml()
            => "<Result success=\"false\"><Error><Type>Input</Type><Message>Invalid input XML. LLMs sometimes emit malformed XML.</Message></Error></Result>";

        private static string MakeOkXml(Action<XmlWriter>? writeBody = null)
        {
            var sb = new StringBuilder(8 * 1024);
            using var xw = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 });
            xw.WriteStartDocument();
            xw.WriteStartElement("Result");
            xw.WriteAttributeString("success", "true");
            xw.WriteAttributeString("timestampUtc", DateTime.UtcNow.ToString("o"));
            writeBody?.Invoke(xw);
            xw.WriteEndElement();
            xw.WriteEndDocument();
            xw.Flush();
            return sb.ToString();
        }

        private static string MakeErrorXml(Exception ex)
        {
            var sb = new StringBuilder(8 * 1024);
            using var xw = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 });
            xw.WriteStartDocument();
            xw.WriteStartElement("Result");
            xw.WriteAttributeString("success", "false");
            xw.WriteAttributeString("timestampUtc", DateTime.UtcNow.ToString("o"));
            xw.WriteStartElement("Error");
            xw.WriteElementString("Type", ex.GetType().FullName ?? "Exception");
            xw.WriteElementString("Message", ex.Message);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                xw.WriteElementString("StackTrace", ex.StackTrace);
            xw.WriteEndElement(); // Error
            xw.WriteEndElement(); // Result
            xw.WriteEndDocument();
            xw.Flush();
            return sb.ToString();
        }

        private static void WriteValueContent(XmlWriter xw, object? value)
        {
            if (value is null || value is DBNull)
            {
                xw.WriteAttributeString("isNull", "true");
                return;
            }

            switch (value)
            {
                case string s:
                    WriteStringSmart(xw, s);
                    break;

                case byte[] bytes:
                    xw.WriteAttributeString("encoding", "base64");
                    xw.WriteCData(Convert.ToBase64String(bytes));
                    break;

                case DateTime dt:
                    xw.WriteAttributeString("format", "o");
                    xw.WriteString(dt.ToUniversalTime().ToString("o"));
                    break;

                case DateTimeOffset dto:
                    xw.WriteAttributeString("format", "o");
                    xw.WriteString(dto.ToUniversalTime().ToString("o"));
                    break;

                case Guid g:
                    xw.WriteString(g.ToString("D"));
                    break;

                case bool b:
                    xw.WriteString(b ? "true" : "false");
                    break;

                case IFormattable f:
                    xw.WriteString(f.ToString(null, System.Globalization.CultureInfo.InvariantCulture));
                    break;

                default:
                    WriteStringSmart(xw, value.ToString() ?? string.Empty);
                    break;
            }
        }

        private static void WriteStringSmart(XmlWriter xw, string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                xw.WriteString(string.Empty);
                return;
            }

            // If the string contains the CDATA terminator, avoid CDATA entirely and let the writer escape it.
            if (s.Contains("]]>", StringComparison.Ordinal))
            {
                xw.WriteString(s);
                return;
            }

            // Otherwise CDATA is fine and keeps embedded markup readable.
            xw.WriteCData(s);
        }

    }
}

