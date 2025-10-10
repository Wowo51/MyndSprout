//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlContain;
using System;

namespace SqlContain.Tests
{
    [TestClass]
    public class DatabaseTrigger_NoIO
    {
        [TestMethod]
        public void DatabaseTriggerSql_MustNotContainIOKeywords_And_ScopeDefaultsToDatabase()
        {
            string sql = DatabaseHardener.GetDatabaseTriggerSql();
            string fallback = DatabaseHardener.GetDatabaseTriggerFallbackSql();

            string[] requiredTokens = new[] { "CREATE TRIGGER", "ON DATABASE", "ROLLBACK", "THROW" };
            foreach (var t in requiredTokens)
            {
                Assert.IsTrue(sql.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0, $"Primary SQL must contain token: {t}");
                Assert.IsTrue(fallback.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0, $"Fallback SQL must contain token: {t}");
            }

            string[] banned = new[] { "xp_cmdshell", "sp_oacreate", "OPENROWSET", "BULK", "xp_reg", "sp_reg", "sp_OA", "xp_" };

            foreach (var b in banned)
            {
                Assert.IsTrue(sql.IndexOf(b, StringComparison.OrdinalIgnoreCase) < 0, $"Primary SQL contains banned token: {b}");
                Assert.IsTrue(fallback.IndexOf(b, StringComparison.OrdinalIgnoreCase) < 0, $"Fallback SQL contains banned token: {b}");
            }

            var opts = new HardenerOptions();
            Assert.AreEqual(Scope.Database, opts.Scope, "HardenerOptions.Scope should default to Database.");
        }
    }
}
