//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System;
using System.Text;
using System.Collections;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class DatabaseHardener_Deny_CreateCredential_Isolation
    {
        [TestMethod]
        public async Task TestMethod()
        {
            // Quick LocalDB availability probe (minimal, local to this test).
            SqlConnectionStringBuilder csbProbe = new SqlConnectionStringBuilder();
            csbProbe.DataSource = "(LocalDB)\\MSSQLLocalDB";
            csbProbe.IntegratedSecurity = true;
            csbProbe.ConnectTimeout = 3;
            csbProbe.InitialCatalog = "master";

            try
            {
                using SqlConnection probeConn = new SqlConnection(csbProbe.ConnectionString);
                await probeConn.OpenAsync();
                probeConn.Close();
            }
            catch (Exception exProbe)
            {
                Assert.Inconclusive("LocalDB unavailable - test requires LocalDB. Diagnostics: " + exProbe.ToString());
                return;
            }

            bool ok = TestSqlProbe.TryGetAvailableMasterConnection(out string masterConn, out string probeMsg);
            if (!ok) { Assert.Inconclusive("LocalDB unavailable - test requires LocalDB"); return; }

            StringBuilder combinedDiagnostics = new StringBuilder();

            await using SqlConnection master = new SqlConnection(masterConn);
            string dbName = $"SqlContain_TestDB_CreateCredential_{Guid.NewGuid():N}";

            bool created = false;

            try
            {
                try
                {
                    await master.OpenAsync();
                }
                catch (Exception exOpen)
                {
                    combinedDiagnostics.AppendLine("Open failed: " + exOpen.ToString());
                    combinedDiagnostics.AppendLine();
                    combinedDiagnostics.AppendLine("ex.Data entries:");
                    foreach (DictionaryEntry d in exOpen.Data)
                    {
                        combinedDiagnostics.AppendLine(d.Key + " = " + d.Value);
                    }
                    Assert.Inconclusive("LocalDB unavailable - test requires LocalDB. Diagnostics: " + combinedDiagnostics.ToString());
                    return;
                }

                try
                {
                    await SqlContain.SqlHelpers.ExecBatchesAsync(master, $"CREATE DATABASE [{dbName}]");
                    created = true;
                }
                catch (Exception exCreate)
                {
                    Assert.Inconclusive("DDL not permitted or failed: " + exCreate.Message);
                    return;
                }

                // Normalize transient DB connection to LocalDB and target the created DB.
                SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder(master.ConnectionString) { DataSource = "(LocalDB)\\MSSQLLocalDB", InitialCatalog = dbName };
                await using SqlConnection dbConn = new SqlConnection(csb.ConnectionString);

                try
                {
                    try
                    {
                        await dbConn.OpenAsync();
                    }
                    catch (Exception exDbOpen)
                    {
                        Assert.Inconclusive("Cannot open DB connection: " + exDbOpen.Message);
                        return;
                    }

                    try
                    {
                        // Use the centralized hardener with AllowSkippedDeny = false to force deterministic behavior.
                        var opts = new HardenerOptions
                        {
                            Database = dbName,
                            Scope = Scope.Database,
                            AllowSkippedDeny = false
                        };

                        try
                        {
                            await DatabaseHardener.HardenAsync(new SqlConnection(masterConn), opts.Database, opts);

                            // Alternate valid outcome: verify via catalog that CREATE CREDENTIAL was applied as DENY.
                            string? stateDesc = await SqlContain.SqlHelpers.ExecScalarAsync<string>(dbConn,
                                "SELECT TOP 1 dp.state_desc FROM sys.database_permissions dp JOIN sys.database_principals p ON dp.grantee_principal_id = p.principal_id " +
                                "WHERE p.name = 'public' AND dp.permission_name = 'CREATE CREDENTIAL';");

                            Assert.AreEqual("DENY", (stateDesc ?? string.Empty).ToUpperInvariant(), "Expected sys.database_permissions state_desc to be DENY for CREATE CREDENTIAL TO public.");
                        }
                        catch (AggregateException ex)
                        {
                            // Preferred fail-closed branch: assert AggregateException includes options/data indicating skipped denies.
                            Assert.IsTrue(ex.Data.Contains("Options.AllowSkippedDeny"));
                            object? rawAllow = ex.Data["Options.AllowSkippedDeny"];
                            bool allowVal = rawAllow is bool ? (bool)rawAllow : false;
                            Assert.AreEqual(false, allowVal);
                            Assert.IsTrue(ex.Data.Contains("SkippedDeny") && ex.Data["SkippedDeny"] != null);

                            // Ensure SkippedDeny contains at least one entry
                            object? raw = ex.Data.Contains("SkippedDeny") ? ex.Data["SkippedDeny"] : null;
                            string[] arr;
                            if (raw is string[] sa) arr = sa;
                            else if (raw is System.Collections.IEnumerable ie) arr = ie.Cast<object>().Select(o => o?.ToString() ?? string.Empty).ToArray();
                            else arr = raw != null ? new string[] { raw.ToString()! } : Array.Empty<string>();

                            Assert.IsTrue(arr.Length > 0, "SkippedDeny should list at least one skipped DENY");

                            // Note: LocalDB/SQL Express may vary in which DENYs are applied; when AllowSkippedDeny=false the test asserts observed behavior explicitly (ex.Data) or verifies catalog.
                        }
                    }
                    catch (Exception ex)
                    {
                        StringBuilder diag = new StringBuilder();
                        diag.AppendLine("Exception.ToString():");
                        diag.AppendLine(ex.ToString());
                        diag.AppendLine();
                        diag.AppendLine("ex.Data entries:");
                        foreach (DictionaryEntry d in ex.Data)
                        {
                            diag.AppendLine(d.Key + " = " + d.Value);
                        }

                        Assert.Fail(diag.ToString());
                        return;
                    }
                }
                finally
                {
                    // dbConn disposed by await using
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
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Best-effort DROP failed for {dbName}: {ex.Message}");
                    }
                }
            }
        }
    }
}
