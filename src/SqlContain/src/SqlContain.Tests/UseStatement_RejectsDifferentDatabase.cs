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
    public class UseStatement_RejectsDifferentDatabase
    {
        [TestMethod]
        public async Task Rejects_Use_For_Different_Database()
        {
            // Use LocalDB only
            var ok = TestSqlProbe.TryGetAvailableMasterConnection(out string masterConn, out string probeMsg);
            if (!ok) { Assert.Inconclusive("LocalDB unavailable - test requires LocalDB"); return; }

            using (SqlConnection master = new SqlConnection(masterConn))
            {
                try
                {
                    master.Open();
                }
                catch (Exception ex)
                {
                    Assert.Inconclusive("SQL server not available: could not open master using LocalDB. " + ex.Message);
                    return;
                }

                using (SqlCommand cmd = master.CreateCommand())
                {
                    cmd.CommandText = "IF DB_ID(N'InitialDb') IS NULL CREATE DATABASE [InitialDb];";
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.ExecuteNonQuery();
                }

                using (SqlCommand cmd2 = master.CreateCommand())
                {
                    cmd2.CommandText = "IF DB_ID(N'OtherDb') IS NULL CREATE DATABASE [OtherDb];";
                    cmd2.CommandType = System.Data.CommandType.Text;
                    cmd2.ExecuteNonQuery();
                }

                bool readyInitial = false;
                bool readyOther = false;
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(30))
                {
                    using (SqlCommand poll = master.CreateCommand())
                    {
                        poll.CommandText = "SELECT state_desc FROM sys.databases WHERE name = N'InitialDb';";
                        poll.CommandType = System.Data.CommandType.Text;
                        object? r1 = poll.ExecuteScalar();
                        if (r1 != null && r1.ToString()!.Equals("ONLINE", StringComparison.OrdinalIgnoreCase)) readyInitial = true;
                    }

                    using (SqlCommand poll2 = master.CreateCommand())
                    {
                        poll2.CommandText = "SELECT state_desc FROM sys.databases WHERE name = N'OtherDb';";
                        poll2.CommandType = System.Data.CommandType.Text;
                        object? r2 = poll2.ExecuteScalar();
                        if (r2 != null && r2.ToString()!.Equals("ONLINE", StringComparison.OrdinalIgnoreCase)) readyOther = true;
                    }

                    if (readyInitial && readyOther) break;
                    Thread.Sleep(200);
                }

                if (!(readyInitial && readyOther))
                {
                    Assert.Inconclusive("InitialDb and/or OtherDb not ONLINE after create attempt.");
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

                    string batchSql = "USE [OtherDb]; SELECT 1;";

                    InvalidOperationException actualEx = await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                    {
                        await SqlHelpers.ExecBatchesAsync(conn, batchSql);
                    });

                    Assert.IsTrue(actualEx.Data.Contains("OffendingSql"), "Exception Data must contain OffendingSql");
                    object? offending = actualEx.Data.Contains("OffendingSql") ? actualEx.Data["OffendingSql"] : null;
                    Assert.IsTrue((offending?.ToString() ?? string.Empty).IndexOf("USE", StringComparison.OrdinalIgnoreCase) >= 0, "OffendingSql must contain 'USE'");

                    Assert.IsTrue(actualEx.Data.Contains("TargetDatabase"), "Exception Data must contain TargetDatabase");
                    Assert.AreEqual("OtherDb", actualEx.Data["TargetDatabase"]);

                    Assert.IsTrue(actualEx.Data.Contains("OriginalInitialCatalog"), "Exception Data must contain OriginalInitialCatalog");
                    Assert.AreEqual("InitialDb", actualEx.Data["OriginalInitialCatalog"]);
                }
                finally
                {
                    // conn disposed by await using
                }
            }
        }
    }
}
