//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MyndSprout
{
    /// <summary>
    /// Generates XSDs for all string-in inputs by delegating to Common.ToXmlSchema.
    /// </summary>
    public static class InputXmlSchemas
    {
        // Per-root helpers (named args so the compiler is happy)
        public static string CreateDatabaseInputXsd() =>
            Common.ToXmlSchema<SqlStrings.CreateDatabaseInput>();

        public static string DropDatabaseInputXsd() =>
            Common.ToXmlSchema<SqlStrings.DropDatabaseInput>();

        public static string ConnectInputXsd() =>
            Common.ToXmlSchema<SqlStrings.ConnectInput>();

        public static string SqlTextRequestXsd() =>
            Common.ToXmlSchema<SqlStrings.SqlTextRequest>();

        public static string SqlXmlRequestXsd() =>
            Common.ToXmlSchema<SqlXmlRequest>(); // from SqlToXml.cs

        public static string EmptyXsd() =>
            Common.ToXmlSchema<Empty>();

        // Bulk helpers
        public static Dictionary<string, string> All() => new()
        {
            ["CreateDatabaseInput"] = CreateDatabaseInputXsd(),
            ["DropDatabaseInput"] = DropDatabaseInputXsd(),
            ["ConnectInput"] = ConnectInputXsd(),
            ["SqlTextRequest"] = SqlTextRequestXsd(),
            ["SqlXmlRequest"] = SqlXmlRequestXsd(),
            ["Empty"] = EmptyXsd(),
        };

        // Marker for <Empty/>
        [XmlRoot("Empty")]
        public sealed class Empty { }
    }
}

