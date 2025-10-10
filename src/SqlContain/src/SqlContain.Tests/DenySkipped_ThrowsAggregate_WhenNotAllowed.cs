//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using HardenerScope = SqlContain.Scope;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class DenySkipped_ThrowsAggregate_WhenNotAllowed
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var ok = TestSqlProbe.TryGetAvailableMasterConnection(out string masterConn, out string probeMsg);
            if (!ok) { Assert.Inconclusive("SQL server not available: " + probeMsg); return; }

            await using SqlConnection master = new SqlConnection(masterConn);
            string dbName = $"SqlContain_TestDB_{Guid.NewGuid():N}";

            HardenerOptions options = new HardenerOptions
            {
                Scope = HardenerScope.Database,
                Database = dbName,
                AllowSkippedDeny = false
            };

            try
            {
                try { await master.OpenAsync(); }
                catch (Exception ex) { Assert.Inconclusive("SQL server not available: " + ex.Message); return; }

                bool created = false;
                try
                {
                    try
                    {
                        await SqlHelpers.ExecBatchesAsync(master, $"CREATE DATABASE [{dbName}]");
                        created = true;
                    }
                    catch (Exception ex) { Assert.Inconclusive("DDL not permitted or failed: " + ex.Message); return; }

                    AggregateException aggEx = await Assert.ThrowsExceptionAsync<AggregateException>(async () =>
                        await DatabaseHardener.HardenAsync(new SqlConnection(masterConn), options.Database, options));
                    Assert.IsTrue(aggEx.Data.Contains("SkippedDeny"), "AggregateException must include SkippedDeny");
                    object? raw = aggEx.Data.Contains("SkippedDeny") ? aggEx.Data["SkippedDeny"] : null;
                    System.Collections.Generic.IEnumerable<string> skipped = (raw as System.Collections.Generic.IEnumerable<string>) ?? (raw != null ? new string[] { raw.ToString()! } : Array.Empty<string>());
                    Assert.IsTrue(skipped.Any(), "SkippedDeny should list at least one skipped DENY");
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
                        catch (Exception ex) { Assert.Inconclusive("DDL cleanup failed: " + ex.Message); }
                    }
                }
            }
            finally { /* connection disposed by await using */ }
        }
    }
}
