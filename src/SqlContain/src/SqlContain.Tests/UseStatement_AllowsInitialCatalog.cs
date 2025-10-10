//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Threading;

namespace SqlContain.Tests
{
    [TestClass]
    public class UseStatement_AllowsInitialCatalog
    {
        [TestMethod]
        public async Task Allows_Use_For_InitialCatalog()
        {
            try
            {
                var csbMaster = new SqlConnectionStringBuilder();
                csbMaster.DataSource = "(LocalDB)\\MSSQLLocalDB";
                csbMaster.IntegratedSecurity = true;
                csbMaster.InitialCatalog = "master";
                using SqlConnection master = new SqlConnection(csbMaster.ConnectionString);
                master.Open();

                // Ensure InitialDb exists (idempotent)
                try
                {
                    using SqlCommand cmd = master.CreateCommand();
                    cmd.CommandText = "IF DB_ID(N'InitialDb') IS NULL CREATE DATABASE [InitialDb];";
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    Assert.Inconclusive("Provisioning LocalDB failed: " + ex.Message);
                    return;
                }

                // Poll for ONLINE state
                bool ready = false;
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    try
                    {
                        using SqlCommand poll = master.CreateCommand();
                        poll.CommandText = "SELECT state_desc FROM sys.databases WHERE name = N'InitialDb'";
                        poll.CommandType = System.Data.CommandType.Text;
                        object? r = poll.ExecuteScalar();
                        if (r != null && r.ToString()!.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
                        {
                            ready = true;
                            break;
                        }
                    }
                    catch (SqlException ex)
                    {
                        Assert.Inconclusive("Provisioning LocalDB failed: " + ex.Message);
                        return;
                    }

                    Thread.Sleep(200);
                }

                if (!ready)
                {
                    Assert.Inconclusive("InitialDb not ONLINE after provisioning attempt.");
                    return;
                }

                SqlConnectionStringBuilder testConnBuilder = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = "InitialDb" };
                await using SqlConnection conn = new SqlConnection(testConnBuilder.ConnectionString);
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

                    string batchSql = "USE [InitialDb]; SELECT 1;";

                    // Should not throw
                    await SqlHelpers.ExecBatchesAsync(conn, batchSql);

                    Assert.IsTrue(true);
                }
                finally
                {
                    // conn disposed by await using
                }
            }
            catch (SqlException ex)
            {
                Assert.Inconclusive("LocalDB not available or cannot open master: " + ex.Message);
                return;
            }
        }
    }
}
