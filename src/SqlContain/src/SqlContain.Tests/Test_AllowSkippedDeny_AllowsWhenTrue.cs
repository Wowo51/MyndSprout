//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class Test_AllowSkippedDeny_AllowsWhenTrue
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var ok = TestSqlProbe.TryGetAvailableMasterConnection(out string masterConn, out string probeMsg);
            if (!ok) { Assert.Inconclusive("SQL server not available: " + probeMsg); return; }

            await using var master = new SqlConnection(masterConn);
            string dbName = $"SqlContain_TestDB_AllowSkippedDeny_{Guid.NewGuid():N}";

            HardenerOptions options = new HardenerOptions
            {
                Scope = SqlContain.Scope.Database,
                Database = string.Empty,
                AllowSkippedDeny = true
            };

            // Opt into skipping trigger creation when environment reports no supported trigger events.
            options.AllowMissingTrigger = true;

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

                options.Database = dbName;

                try
                {
                    await DatabaseHardener.HardenAsync(new SqlConnection(masterConn), options.Database, options);
                }
                catch (AggregateException agg)
                {
                    Assert.Fail("AggregateException thrown despite AllowSkippedDeny==true. Data keys: " + string.Join(",", agg.Data.Keys.Cast<object>()));
                }
                catch (Exception ex)
                {
                    Assert.Fail("Unexpected exception: " + ex.Message);
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
                    catch (Exception) { /* best-effort cleanup; ignore */ }
                }
            }
        }
    }
}
