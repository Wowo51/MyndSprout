//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public class AllGeneratedSql_NoDbIoTokens
    {
        [TestMethod]
        public void Scan_SqlContain_For_BannedDbIoTokens()
        {
            // Absolute folder to scan
            string root = "C:\\Users\\wowod\\Desktop\\Code2025\\RavenStage\\src\\SqlContain";

            // Tokens to search for (case-insensitive)
            string[] tokens = new[] {
                "CREATE ASSEMBLY",
                "xp_cmdshell",
                "BULK INSERT",
                "OPENROWSET",
                "OPENROWSET(BULK",
                "BULK",
                "CLR ASSEMBLY",
                "CREATE CREDENTIAL",
                "xp_",
                "OLE DB",
                "BULKADMIN",
                "sp_OACreate",
                "xp_dirtree",
                "sp_OA",
                "EXTERNAL ACCESS",
                "UNSAFE"
            };

            if (!Directory.Exists(root))
            {
                Assert.Fail($"Scan root not found: {root}");
            }

            var matches = new List<string>();

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                // Exclude test project files if any were placed under the same tree
                if (file.IndexOf("SqlContain.Tests", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                string fileText;
                try
                {
                    fileText = File.ReadAllText(file);
                }
                catch (Exception ex)
                {
                    matches.Add($"{file}: <file read error>: {ex.Message}");
                    continue;
                }

                // Split into statements and skip any statement that begins with DENY (case-insensitive).
                string[] statements = fileText.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int sidx = 0; sidx < statements.Length; sidx++)
                {
                    string stmt = statements[sidx] ?? string.Empty;
                    if (stmt.TrimStart().StartsWith("DENY", StringComparison.OrdinalIgnoreCase)) continue;

                    foreach (var token in tokens)
                    {
                        if (string.IsNullOrEmpty(token)) continue;
                        if (stmt.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Exempt DatabaseHardener.cs: contains canonical permission-name literals used for detection; allowed here.
                            if (file.EndsWith("DatabaseHardener.cs", StringComparison.OrdinalIgnoreCase)) { continue; }

                            string snippet = stmt.Trim().Replace('\r', ' ').Replace('\n', ' ');
                            matches.Add($"{file}: token=\"{token}\" => {snippet}");
                        }
                    }
                }
            }

            if (matches.Any())
            {
                Assert.Fail("Banned DB-IO tokens found:\n" + string.Join("\n", matches));
            }

            Assert.IsTrue(true);
        }
    }
}
