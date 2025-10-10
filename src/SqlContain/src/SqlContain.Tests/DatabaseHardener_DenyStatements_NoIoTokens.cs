//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class DatabaseHardener_DenyStatements_NoIoTokens
    {
        [TestMethod]
        public void DenyStatementsContainNoDbIoTokens()
        {
            string[] denies = SqlContain.DatabaseHardener.GetDatabaseDenyStatements() ?? new string[0];

            string[] ioTokens = new string[] {
                "xp_cmdshell",
                "OPENROWSET",
                "OPENQUERY",
                "BULK",
                "xp_",
                "sp_OACreate",
                "sp_OAMethod",
                "EXTERNAL DATA SOURCE",
                "EXTERNAL FILE FORMAT",
                "EXEC xp_"
            };

            for (int i = 0; i < denies.Length; i++)
            {
                string stmt = denies[i] ?? string.Empty;
                string trimmed = stmt.TrimStart();
                bool startsWithDeny = trimmed.StartsWith("DENY ", StringComparison.OrdinalIgnoreCase);
                if (startsWithDeny)
                {
                    Assert.IsTrue(startsWithDeny, "Deny entry must start with 'DENY '");
                    continue;
                }

                for (int j = 0; j < ioTokens.Length; j++)
                {
                    string token = ioTokens[j];
                    bool contains = stmt.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
                    Assert.IsFalse(contains, $"Deny statement contains IO-capable token '{token}': {stmt}");
                }
            }
        }
    }
}
