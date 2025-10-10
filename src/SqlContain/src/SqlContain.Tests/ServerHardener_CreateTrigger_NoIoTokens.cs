//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class ServerHardener_CreateTrigger_NoIoTokens
    {
        [TestMethod]
        public void ServerTriggerContainsNoUnsafeIoOrEnsuresBlocking()
        {
            string sql = SqlContain.ServerHardener.GetServerTriggerSql() ?? string.Empty;

            string[] forbiddenTokens = new string[] {
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

            for (int i = 0; i < forbiddenTokens.Length; i++)
            {
                string t = forbiddenTokens[i];
                bool found = sql.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
                Assert.IsFalse(found, $"Server trigger SQL contains unsafe token '{t}'. snippet: ...{(sql.Length>200 ? sql.Substring(0,200) : sql)}...");
            }

            string[] intentionallyTargetedTokens = new string[] {
                "CREATE_LINKED_SERVER",
                "CREATE_LINKEDSERVER",
                "CREATE_CREDENTIAL",
                "CREATE CREDENTIAL",
                "ALTER_CREDENTIAL",
                "ALTER CREDENTIAL"
            };

            bool hasBlocking = sql.IndexOf("ROLLBACK", StringComparison.OrdinalIgnoreCase) >= 0
                || sql.IndexOf("THROW", StringComparison.OrdinalIgnoreCase) >= 0
                || sql.IndexOf("RAISERROR", StringComparison.OrdinalIgnoreCase) >= 0;

            for (int i = 0; i < intentionallyTargetedTokens.Length; i++)
            {
                string t = intentionallyTargetedTokens[i];
                if (sql.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Assert.IsTrue(hasBlocking, $"Server trigger contains targeted token '{t}' but no blocking logic (ROLLBACK/THROW/RAISERROR) found. snippet: ...{(sql.Length>200 ? sql.Substring(0,200) : sql)}...");
                }
            }
        }
    }
}
