//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MyndSprout
{
    public class Common
    {
        public static T? FromXml<T>(string text)
        {
            string? xml = ExtractXml(text);
            if (xml == null)
            {
                return default(T);
            }
            T? t = FromXmlOnly<T>(xml);
            return t;
        }

        public static string? ExtractXml(string input)
        {
            string pattern = @"(?s)(?:<\?xml.*?\?>\s*)?(?<xml><(?<tag>[a-zA-Z0-9_:\-]+)[^>]*(?:\s*/>|>(?:.*?</\k<tag>>)))";

            Match match = Regex.Match(input, pattern);
            if (match.Success)
            {
                return match.Groups["xml"].Value;
            }
            return null;
        }

        public static T? FromXmlOnly<T>(string xmlContent)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (StringReader reader = new StringReader(xmlContent))
                {
                    return (T?)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return default(T);
            }
        }

        /// <summary>
        /// Generates a single, self-contained XSD for <typeparamref name="TRoot"/>
        /// with extra annotations that spell out list/array item names - the part
        /// that typically trips up an LLM.
        /// </summary>
        public static string ToXmlSchema<TRoot>(
            XmlAttributeOverrides? overrides = null,
            bool indent = true)
        {
            // ----------------------------------------------------------
            // 1. Reflect & export
            // ----------------------------------------------------------
            XmlReflectionImporter importer = overrides == null
                ? new XmlReflectionImporter()
                : new XmlReflectionImporter(overrides);

            XmlTypeMapping mapping = importer.ImportTypeMapping(typeof(TRoot));
            XmlSchemas schemas = new XmlSchemas();
            XmlSchemaExporter exporter = new XmlSchemaExporter(schemas);

            exporter.ExportTypeMapping(mapping);
            schemas.Compile(null, true);                      // merge + validate

            // ----------------------------------------------------------
            // 2. Add handy <xs:documentation> comments for lists/arrays
            // ----------------------------------------------------------
            // Annotate each schema produced
            // (keep AnnotateCollections invocation but apply per-schema)
            
            // ----------------------------------------------------------
            // 3. Dump to string (per-schema writer with Document conformance)
            // ----------------------------------------------------------
            var sb = new StringBuilder(32 * 1024);

            foreach (XmlSchema s in schemas)
            {
                AnnotateCollections(s);

                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = indent,
                    OmitXmlDeclaration = true,
                    ConformanceLevel = ConformanceLevel.Document
                };

                using (XmlWriter writer = XmlWriter.Create(sb, settings))
                {
                    s.Write(writer);
                    writer.Flush();
                }
            }

            return sb.ToString();
        }

        /* ========== helpers ========== */

        private static void AnnotateCollections(XmlSchema schema)
        {
            foreach (XmlSchemaElement root in schema.Elements.Values)
                if (root.ElementSchemaType is XmlSchemaComplexType ct)
                    AnnotateComplexType(ct, schema);
        }

        private static void AnnotateComplexType(
            XmlSchemaComplexType ct,
            XmlSchema schema)
        {
            if (ct.ContentTypeParticle is XmlSchemaSequence seq)
            {
                foreach (var item in seq.Items.OfType<XmlSchemaElement>())
                {
                    // Determine if the element represents a list
                    bool isList = item.MaxOccurs > 1 ||
                                  item.MaxOccursString == "unbounded";

                    if (isList)
                    {
                        string itemTypeName = GetElementTypeName(item, schema);
                        AddDocumentation(item,
                            $"LIST OF {itemTypeName}");
                    }

                    if (item.ElementSchemaType is XmlSchemaComplexType nestedCt)
                        AnnotateComplexType(nestedCt, schema);
                }
            }
        }

        private static string GetElementTypeName(XmlSchemaElement el, XmlSchema schema)
        {
            // Try local type first
            if (!string.IsNullOrEmpty(el.SchemaTypeName.Name))
                return el.SchemaTypeName.Name;

            // Fall back to anonymous complex type name, if any
            return el.ElementSchemaType?.Name ?? "UNKNOWN";
        }

        private static void AddDocumentation(XmlSchemaAnnotated node, string text)
        {
            // Avoid duplicates
            bool already = node.Annotation?
                .Items.OfType<XmlSchemaDocumentation>()
                .Any(d => d.Markup?.Any(m => m!.InnerText == text) == true) == true;

            if (already) return;

            var doc = new XmlSchemaDocumentation();
            doc.Markup = new[] { new XmlDocument().CreateTextNode(text) };

            node.Annotation ??= new XmlSchemaAnnotation();
            node.Annotation.Items.Add(doc);
        }

        public static string WrapInTags(string body, string tagName)
        {
            List<string> block = new List<string>();
            block.Add("<" + tagName + ">");
            if (body == "")
            {
                block.Add("No data available.");
            }
            else
            {
                block.Add(body);
            }
            block.Add("</" + tagName + ">");
            return string.Join(Environment.NewLine, block);
        }
    }
}

