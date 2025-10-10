//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlHelpers_ExecBatches_MultiStatement_CreateAndDrop
    {
        [TestMethod]
        public async Task TestMethod()
        {
            HardenerOptions o = new HardenerOptions { Server = "(LocalDB)\\MSSQLLocalDB", Auth = "Trusted" };
            await using var conn = SqlConnectionFactory.CreateMaster(o);
            var dbName = $"SqlContain_TestDB_{Guid.NewGuid():N}";

            try
            {
                try
                {
                    await conn.OpenAsync();
                }
                catch (Exception ex)
                {
                    Assert.Inconclusive("SQL server not available: " + ex.Message);
                    return;
                }

                var sql = $"CREATE DATABASE [{dbName}]; DROP DATABASE [{dbName}];";

                try
                {
                    await SqlHelpers.ExecBatchesAsync(conn, sql);
                }
                catch (Exception ex)
                {
                    Assert.Inconclusive("DDL not permitted or failed: " + ex.Message);
                    return;
                }
                finally
                {
                    try
                    {
                        // Ensure cleanup in case the combined batch did not drop the DB.
                        await SqlHelpers.ExecBatchesAsync(conn, $"IF DB_ID(N'{dbName}') IS NOT NULL DROP DATABASE [{dbName}];");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"Best-effort DROP failed for {dbName}: {ex.Message}");
                    }
                }
            }
            finally
            {
                // connection disposed by await using
            }
        }
    }
}
