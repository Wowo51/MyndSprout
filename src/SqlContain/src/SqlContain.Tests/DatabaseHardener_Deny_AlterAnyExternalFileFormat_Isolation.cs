//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class DatabaseHardener_Deny_AlterAnyExternalFileFormat_Isolation
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var o = new HardenerOptions { Server = "(LocalDB)\\MSSQLLocalDB", Auth = "Trusted" };
            await using var master = SqlConnectionFactory.CreateMaster(o);
            var dbName = $"SqlContain_Isolation_AlterAnyExternalFileFormat_{Guid.NewGuid():N}";

            try
            {
                try { await master.OpenAsync(); }
                catch (Exception ex) { Assert.Inconclusive("SQL server not available: " + ex.Message); return; }

                bool created = false;
                try
                {
                    await SqlHelpers.ExecBatchesAsync(master, $"CREATE DATABASE [{dbName}]");
                    created = true;
                }
                catch (Exception ex) { Assert.Inconclusive("DDL not permitted or failed: " + ex.Message); return; }

                try
                {
                    var csb = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = dbName };
                    await using var dbConn = new SqlConnection(csb.ConnectionString);
                    await dbConn.OpenAsync();

                    try
                    {
                        await SqlHelpers.ExecBatchesAsync(dbConn, "DENY ALTER ANY EXTERNAL FILE FORMAT TO public;");
                        Assert.IsTrue(true);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail("DENY failed: " + ex.Message + " -- SQL: " + (ex.Data["Sql"] ?? ""));
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
                        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Best-effort DROP failed for {dbName}: {ex.Message}"); }
                    }
                }
            }
            finally { }
        }
    }
}
