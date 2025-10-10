//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class DatabaseHardener_Scoping_DatabaseOnly
    {
        [TestMethod]
        public void DatabaseTrigger_IsDatabaseScoped_And_NoServerTokens()
        {
            string sql = SqlContain.DatabaseHardener.GetDatabaseTriggerSql();
            if (string.IsNullOrEmpty(sql)) sql = SqlContain.DatabaseHardener.GetDatabaseTriggerFallbackSql();
            if (string.IsNullOrEmpty(sql)) Assert.Fail("Database trigger SQL accessor(s) returned null or empty. Ensure GetDatabaseTriggerSql and/or GetDatabaseTriggerFallbackSql are present and return SQL to inspect.");

            string snippet = sql.Length > 200 ? sql.Substring(0,200) : sql;

            bool hasCreate = sql.IndexOf("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasOnDatabase = sql.IndexOf("ON DATABASE", StringComparison.OrdinalIgnoreCase) >= 0;
            Assert.IsTrue(hasCreate && hasOnDatabase, "Database trigger SQL does not appear to target database scope (missing 'CREATE TRIGGER' and/or 'ON DATABASE'). snippet: ..." + snippet + "...");

            string[] serverTokens = new string[] { "ON ALL SERVER", "ALTER SERVER", "ALTER ANY SERVER", "SP_CONFIGURE", "SP_SERVEROPTION" };
            for (int i = 0; i < serverTokens.Length; i++)
            {
                string t = serverTokens[i];
                bool found = sql.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0;
                Assert.IsFalse(found, "Database trigger SQL contains server-scoped token '" + t + "'. snippet: ..." + snippet + "...");
            }
        }
    }
}
