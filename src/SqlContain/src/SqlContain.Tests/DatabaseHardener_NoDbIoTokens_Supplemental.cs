//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public class DatabaseHardener_NoDbIoTokens_Supplemental
    {
        [TestMethod]
        public void NoDbIoTokensInGeneratedSql()
        {
            string triggerSql = SqlContain.DatabaseHardener.GetDatabaseTriggerSql();
            string fallbackSql = SqlContain.DatabaseHardener.GetDatabaseTriggerFallbackSql();
            string serverSql = SqlContain.ServerHardener.GetServerTriggerSql();

            string[] candidates = new string[] { triggerSql, fallbackSql, serverSql };

            string[] ioTokens = new string[] {
                "xp_cmdshell",
                "OPENROWSET",
                "OPENQUERY",
                "BULK",
                "xp_",
                "sp_OACreate",
                "sp_OAMethod",
                "EXTERNAL FILE FORMAT",
                "EXEC xp_"
            };

            string[] intentionallyTargetedTokens = new string[] {
                "CREATE ASSEMBLY",
                "EXTERNAL DATA SOURCE",
                "EXTERNAL FILE FORMAT",
                "CREATE CREDENTIAL",
                "ALTER CREDENTIAL",
                "CREATE_EXTERNAL_DATA_SOURCE",
                "CREATE_EXTERNAL_FILE_FORMAT",
                "CREATE_EXTERNAL_LIBRARY"
            };

            string[] blockingStatements = new string[] { "ROLLBACK", "THROW", "RAISERROR" };

            for (int i = 0; i < candidates.Length; i++)
            {
                string sql = candidates[i];
                if (string.IsNullOrEmpty(sql))
                {
                    string which = i == 0 ? "DatabaseTriggerSql" : (i == 1 ? "DatabaseTriggerFallbackSql" : "ServerTriggerSql");
                    Assert.Fail(which + " returned null or empty; expected SQL to inspect.");
                }

                for (int j = 0; j < ioTokens.Length; j++)
                {
                    string token = ioTokens[j];
                    bool found = sql.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
                    Assert.IsFalse(found, $"Generated SQL contains IO-capable token '{token}'. snippet: ...{(sql.Length>200 ? sql.Substring(0,200) : sql)}...");
                }

                bool hasBlocking = sql.IndexOf("ROLLBACK", StringComparison.OrdinalIgnoreCase) >= 0
                    || sql.IndexOf("THROW", StringComparison.OrdinalIgnoreCase) >= 0
                    || sql.IndexOf("RAISERROR", StringComparison.OrdinalIgnoreCase) >= 0;

                for (int k = 0; k < intentionallyTargetedTokens.Length; k++)
                {
                    string tk = intentionallyTargetedTokens[k];
                    bool present = sql.IndexOf(tk, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (present)
                    {
                        Assert.IsTrue(hasBlocking, $"SQL contains targeted token '{tk}' but no blocking logic (ROLLBACK/THROW/RAISERROR) found. snippet: ...{(sql.Length>200 ? sql.Substring(0,200) : sql)}...");
                    }
                }
            }
        }
    }
}
