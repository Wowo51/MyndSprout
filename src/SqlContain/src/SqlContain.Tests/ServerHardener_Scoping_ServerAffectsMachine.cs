//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class ServerHardener_Scoping_ServerAffectsMachine
    {
        [TestMethod]
        public void ServerTrigger_IsServerScoped_And_HasBlockingLogic()
        {
            string sql = SqlContain.ServerHardener.GetServerTriggerSql();
            if (string.IsNullOrEmpty(sql)) Assert.Fail("Server trigger SQL accessor returned null or empty. Ensure GetServerTriggerSql is present and returns SQL to inspect.");

            string snippet = sql.Length > 200 ? sql.Substring(0,200) : sql;

            bool hasCreate = sql.IndexOf("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasOnAllServer = sql.IndexOf("ON ALL SERVER", StringComparison.OrdinalIgnoreCase) >= 0 || sql.IndexOf("ALL SERVER", StringComparison.OrdinalIgnoreCase) >= 0;
            Assert.IsTrue(hasCreate && hasOnAllServer, "Server trigger SQL does not appear to target server scope. This is an opt-in behavior if present. snippet: ..." + snippet + "...");

            bool hasBlocking = sql.IndexOf("ROLLBACK", StringComparison.OrdinalIgnoreCase) >= 0
                || sql.IndexOf("THROW", StringComparison.OrdinalIgnoreCase) >= 0
                || sql.IndexOf("RAISERROR", StringComparison.OrdinalIgnoreCase) >= 0;

            Assert.IsTrue(hasBlocking, "Server trigger SQL appears to affect the instance but does not contain blocking/permission-checking constructs (ROLLBACK/THROW/RAISERROR). snippet: ..." + snippet + "...");
        }
    }
}
