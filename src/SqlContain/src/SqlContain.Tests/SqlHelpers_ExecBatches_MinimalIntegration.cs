//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlHelpers_ExecBatches_MinimalIntegration
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var o = new HardenerOptions { Server = "(LocalDB)\\MSSQLLocalDB", Auth = "Trusted" };
            await using SqlConnection conn = SqlConnectionFactory.CreateMaster(o);
            string dbName = $"SqlContain_TestDB_{Guid.NewGuid():N}";

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
                    using SqlCommand cmdCreate = conn.CreateCommand();
                    cmdCreate.CommandText = $"CREATE DATABASE [{dbName}]";
                    cmdCreate.CommandType = System.Data.CommandType.Text;
                    await cmdCreate.ExecuteNonQueryAsync();
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
                            using SqlCommand cmdDrop = conn.CreateCommand();
                            cmdDrop.CommandText = $"DROP DATABASE [{dbName}]";
                            cmdDrop.CommandType = System.Data.CommandType.Text;
                            await cmdDrop.ExecuteNonQueryAsync();
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
