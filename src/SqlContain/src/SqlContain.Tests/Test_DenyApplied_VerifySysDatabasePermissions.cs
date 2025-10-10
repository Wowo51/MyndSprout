//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class Test_DenyApplied_VerifySysDatabasePermissions
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var ok = TestSqlProbe.TryGetAvailableMasterConnection(out string masterConn, out string probeMsg);
            if (!ok) { Assert.Inconclusive("SQL server not available: " + probeMsg); return; }

            await using var master = new SqlConnection(masterConn);
            string dbName = $"SqlContain_TestDB_DenyVerify_{Guid.NewGuid():N}";
            bool created = false;

            try
            {
                try { await master.OpenAsync(); }
                catch (Exception ex) { Assert.Inconclusive("SQL server not available: " + ex.Message); return; }

                try
                {
                    await SqlHelpers.ExecBatchesAsync(master, $"CREATE DATABASE [{dbName}]");
                    created = true;
                }
                catch (Exception ex) { Assert.Inconclusive("DDL not permitted or failed: " + ex.Message); return; }

                var csb = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = dbName };
                await using var dbConn = new SqlConnection(csb.ConnectionString);
                try
                {
                    await dbConn.OpenAsync();
                }
                catch (Exception ex) { Assert.Inconclusive("Cannot open DB connection: " + ex.Message); return; }

                try
                {
                    // Apply DENY expected to succeed
                    await SqlHelpers.ExecBatchesAsync(dbConn, "DENY CREATE ASSEMBLY TO public;");
                }
                catch (Exception ex)
                {
                    Assert.Inconclusive("DENY could not be applied in this environment: " + ex.Message);
                    return;
                }

                try
                {
                    int found = await SqlHelpers.ExecScalarAsync<int>(dbConn,
                        "SELECT COUNT(*) FROM sys.database_permissions dp JOIN sys.database_principals p ON dp.grantee_principal_id = p.principal_id " +
                        "WHERE dp.state_desc = 'DENY' AND p.name = 'public' AND dp.permission_name = 'CREATE ASSEMBLY';");
                    Assert.IsTrue(found > 0, "Expected sys.database_permissions to contain a DENY row for CREATE ASSEMBLY TO public.");
                }
                catch (Exception ex)
                {
                    Assert.Fail("Verification query failed: " + ex.Message);
                }
            }
            finally
            {
                if (created)
                {
                    try
                    {
                        string dropSql = $"IF DB_ID(N'{dbName}') IS NOT NULL BEGIN ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\nDROP DATABASE [{dbName}]; END";
                        await SqlHelpers.ExecBatchesAsync(master, dropSql);
                    }
                    catch (Exception) { /* best-effort */ }
                }
            }
        }
    }
}
