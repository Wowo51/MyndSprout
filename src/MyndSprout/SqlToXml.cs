//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.SqlClient;

namespace MyndSprout
{
    /// <summary>
    /// Bundles all inputs for ExecuteToXmlAsync into a single object.
    /// </summary>
    public sealed class SqlXmlRequest
    {
        public string Sql { get; init; } = string.Empty;
        // Use a serializable DTO instead of SqlParameter to allow XSD generation.
        public XmlSerializableSqlParameter[]? Parameters { get; init; }
        public CommandType CommandType { get; init; } = CommandType.Text;
        public int? CommandTimeoutSeconds { get; init; }
        public bool IsSchemaRequest { get; init; } = false;
}

    // Serializable parameter DTO (XML-friendly)
    public sealed class XmlSerializableSqlParameter
    {
        public string ParameterName { get; set; } = string.Empty;
        public string? SqlDbType { get; set; }
        public string Direction { get; set; } = "Input";
        public int? Size { get; set; }
        public byte? Precision { get; set; }
        public byte? Scale { get; set; }
        public string? Value { get; set; }
        public bool IsNull { get; set; } = false;
    }

    public partial class SqlService
    {
        public async Task<string> ExecuteToXmlAsync(string inXml)
        {
            SqlXmlRequest? request = Common.FromXml<SqlXmlRequest>(inXml);
            if (request == null) return "Invalid input XML. LLM's occasionaly fail to write valid xml that is normal.";
            return await ExecuteToXmlAsync(request);
        }


        public async Task<string> ExecuteToXmlAsync(SqlXmlRequest request)
        {
            if (Database == null) throw new InvalidOperationException("Not connected.");
            if (request is null) throw new ArgumentNullException(nameof(request));

            var messages = new List<string>();
            SqlInfoMessageEventHandler? handler = (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Message)) messages.Add(e.Message);
            };
            Database.InfoMessage += handler;

            try
            {
                await using var cmd = new SqlCommand(request.Sql, Database)
                {
                    CommandType = request.CommandType
                };
                if (request.CommandTimeoutSeconds is int t) cmd.CommandTimeout = t;

                if (request.Parameters != null)
                {
                    foreach (XmlSerializableSqlParameter dto in request.Parameters)
                    {
                        SqlParameter param = MakeSqlParameter(dto);
                        cmd.Parameters.Add(param);
                    }
                }

                // Auto add return value for procs if not present
                if (request.CommandType == CommandType.StoredProcedure &&
                    !cmd.Parameters.Contains("@RETURN_VALUE"))
                {
                    cmd.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.ReturnValue
                    });
                }

                using var sw = new StringWriter(new StringBuilder(64 * 1024));
                using (var xw = XmlWriter.Create(sw, new XmlWriterSettings
                {
                    Indent = true,
                    OmitXmlDeclaration = false,
                    Encoding = Encoding.UTF8
                }))
                {
                    xw.WriteStartDocument();
                    xw.WriteStartElement("SqlResult");
                    xw.WriteAttributeString("timestampUtc", DateTime.UtcNow.ToString("o"));
                    xw.WriteAttributeString("database", Database.Database ?? "");
                    xw.WriteAttributeString("dataSource", Database.DataSource ?? "");

                    // Echo command
                    xw.WriteStartElement("Command");
                    xw.WriteAttributeString("type", request.CommandType.ToString());
                    xw.WriteElementString("Text", request.Sql);
                    if (cmd.Parameters.Count > 0)
                    {
                        xw.WriteStartElement("Parameters");
                        foreach (SqlParameter p in cmd.Parameters)
                        {
                            xw.WriteStartElement("Parameter");
                            xw.WriteAttributeString("name", p.ParameterName);
                            xw.WriteAttributeString("direction", p.Direction.ToString());
                            xw.WriteAttributeString("dbType", p.SqlDbType.ToString());
                            if (p.Size > 0) xw.WriteAttributeString("size", p.Size.ToString());
                            if (p.Precision > 0) xw.WriteAttributeString("precision", p.Precision.ToString());
                            if (p.Scale > 0) xw.WriteAttributeString("scale", p.Scale.ToString());
                            if (p.Direction is ParameterDirection.Input or ParameterDirection.InputOutput)
                                WriteValueElement(xw, "Value", p.Value);
                            xw.WriteEndElement();
                        }
                        xw.WriteEndElement(); // Parameters
                    }
                    xw.WriteEndElement(); // Command

                    // Execute & capture all result sets
                    int resultSetIndex = 0;
                    int recordsAffected;
                    await using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        do
                        {
                            var schema = reader.GetColumnSchema();

                            xw.WriteStartElement("ResultSet");
                            xw.WriteAttributeString("index", resultSetIndex.ToString());

                            xw.WriteStartElement("Columns");
                            for (int i = 0; i < schema.Count; i++)
                            {
                                var c = schema[i];
                                xw.WriteStartElement("Column");
                                xw.WriteAttributeString("ordinal", (c.ColumnOrdinal ?? i).ToString());
                                xw.WriteAttributeString("name", c.ColumnName ?? $"Column{i}");
                                xw.WriteAttributeString("type", c.DataTypeName ?? c.DataType?.Name ?? "unknown");
                                if (c.AllowDBNull.HasValue) xw.WriteAttributeString("allowNull", c.AllowDBNull.Value ? "true" : "false");
                                if (c.ColumnSize.HasValue) xw.WriteAttributeString("length", c.ColumnSize.Value.ToString());
                                if (c.NumericPrecision.HasValue) xw.WriteAttributeString("precision", c.NumericPrecision.Value.ToString());
                                if (c.NumericScale.HasValue) xw.WriteAttributeString("scale", c.NumericScale.Value.ToString());
                                if (!string.IsNullOrWhiteSpace(c.BaseTableName))
                                {
                                    xw.WriteAttributeString("baseSchema", c.BaseSchemaName ?? "");
                                    xw.WriteAttributeString("baseTable", c.BaseTableName ?? "");
                                }
                                xw.WriteEndElement(); // Column
                            }
                            xw.WriteEndElement(); // Columns

                            xw.WriteStartElement("Rows");
                            while (await reader.ReadAsync())
                            {
                                xw.WriteStartElement("Row");
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    xw.WriteStartElement("Col");
                                    var name = reader.GetName(i);
                                    xw.WriteAttributeString("name", string.IsNullOrEmpty(name) ? $"Column{i}" : name);
                                    xw.WriteAttributeString("type", reader.GetDataTypeName(i));
                                    if (await reader.IsDBNullAsync(i))
                                    {
                                        xw.WriteAttributeString("isNull", "true");
                                    }
                                    else
                                    {
                                        WriteTypedValue(xw, await reader.IsDBNullAsync(i) ? null : reader.GetValue(i));
                                    }
                                    xw.WriteEndElement(); // Col
                                }
                                xw.WriteEndElement(); // Row
                            }
                            xw.WriteEndElement(); // Rows

                            xw.WriteEndElement(); // ResultSet
                            resultSetIndex++;
                        }
                        while (await reader.NextResultAsync());

                        recordsAffected = reader.RecordsAffected; // -1 if none/DDL
                    }

                    // Summary
                    xw.WriteStartElement("Summary");
                    xw.WriteElementString("RecordsAffected", recordsAffected.ToString());
                    xw.WriteEndElement();

                    // Output parameters & return value
                    if (cmd.Parameters.Count > 0)
                    {
                        xw.WriteStartElement("OutputParameters");
                        foreach (SqlParameter p in cmd.Parameters)
                        {
                            if (p.Direction is ParameterDirection.Output or ParameterDirection.InputOutput or ParameterDirection.ReturnValue)
                            {
                                xw.WriteStartElement("Parameter");
                                xw.WriteAttributeString("name", p.ParameterName);
                                xw.WriteAttributeString("direction", p.Direction.ToString());
                                xw.WriteAttributeString("dbType", p.SqlDbType.ToString());
                                WriteValueElement(xw, "Value", p.Value);
                                xw.WriteEndElement();
                            }
                        }
                        xw.WriteEndElement();
                    }

                    // Messages from PRINT/RAISERROR (low severity)
                    if (messages.Count > 0)
                    {
                        xw.WriteStartElement("Messages");
                        foreach (var m in messages) xw.WriteElementString("Message", m);
                        xw.WriteEndElement();
                    }

                    xw.WriteEndElement(); // SqlResult
                    xw.WriteEndDocument();
                    xw.Flush();
                }

                return sw.ToString();
            }
            catch (Exception ex)
            {
                var err = new StringBuilder();
                using var xw = XmlWriter.Create(err, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 });
                xw.WriteStartDocument();
                xw.WriteStartElement("SqlResult");
                xw.WriteAttributeString("timestampUtc", DateTime.UtcNow.ToString("o"));
                xw.WriteStartElement("Error");
                xw.WriteElementString("Type", ex.GetType().FullName ?? "Exception");
                xw.WriteElementString("Message", ex.Message);
                xw.WriteElementString("StackTrace", ex.StackTrace ?? "");
                xw.WriteEndElement(); // Error
                if (messages.Count > 0)
                {
                    xw.WriteStartElement("Messages");
                    foreach (var m in messages) xw.WriteElementString("Message", m);
                    xw.WriteEndElement();
                }
                xw.WriteEndElement(); // SqlResult
                xw.WriteEndDocument();
                xw.Flush();
                return err.ToString();
            }
            finally
            {
                Database.InfoMessage -= handler;
            }
        }

        // Helper to convert the serializable DTO into a real SqlParameter
        private static SqlParameter MakeSqlParameter(XmlSerializableSqlParameter dto)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = string.IsNullOrEmpty(dto.ParameterName) ? string.Empty : dto.ParameterName;

            ParameterDirection dir;
            if (Enum.TryParse<ParameterDirection>(dto.Direction, true, out dir))
            {
                param.Direction = dir;
            }
            else
            {
                param.Direction = ParameterDirection.Input;
            }

            if (!string.IsNullOrEmpty(dto.SqlDbType))
            {
                SqlDbType parsedDbType;
                if (Enum.TryParse<SqlDbType>(dto.SqlDbType, true, out parsedDbType))
                {
                    param.SqlDbType = parsedDbType;
                }
            }

            if (dto.Size.HasValue) param.Size = dto.Size.Value;
            if (dto.Precision.HasValue) param.Precision = dto.Precision.Value;
            if (dto.Scale.HasValue) param.Scale = dto.Scale.Value;

            if (dto.IsNull)
            {
                param.Value = DBNull.Value;
            }
            else
            {
                param.Value = (object?)(dto.Value ?? string.Empty);
            }

            return param;
        }

        // --- helpers (keep inside the class) ---

        private static void WriteValueElement(XmlWriter xw, string elementName, object? value)
        {
            xw.WriteStartElement(elementName);
            WriteTypedValue(xw, value);
            xw.WriteEndElement();
        }

        private static void WriteTypedValue(XmlWriter xw, object? value)
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

            // Avoid CDATA when the value itself contains the CDATA terminator.
            if (s.Contains("]]>", StringComparison.Ordinal))
            {
                xw.WriteString(s);
                return;
            }

            xw.WriteCData(s);
        }

    }

    public static class SqlToXml
    {
        public static SqlParameter MakeSqlParameter(string name, object? value)
        {
            ArgumentNullException.ThrowIfNull(name);
            SqlParameter param = new SqlParameter
            {
                ParameterName = string.IsNullOrEmpty(name) ? string.Empty : name,
                Value = value ?? DBNull.Value
            };
            return param;
        }
    }
}

