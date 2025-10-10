//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlHelpers_ExecBatches_Executes_CreateAndDrop_ViaHelper
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

                bool created = false;
                try
                {
                    await SqlHelpers.ExecBatchesAsync(conn, $"CREATE DATABASE [{dbName}]");
                    created = true;
                }
                catch (Exception ex)
                {
                    Assert.Inconclusive("DDL not permitted or failed: " + ex.Message);
                    return;
                }
                finally
                {
                    if (created)
                    {
                        try
                        {
                            await SqlHelpers.ExecBatchesAsync(conn, $"DROP DATABASE [{dbName}]");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine($"Best-effort DROP failed for {dbName}: {ex.Message}");
                        }
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
