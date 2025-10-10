//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using MyndSprout;
using Microsoft.Data.SqlClient;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlService_GetSchemaAsync_ReturnsExpectedSections_LocalDb_Tests
    {
        [TestMethod]
        public async Task GetSchemaAsync_ReturnsExpectedSections_LocalDb()
        {
            // Use LocalDB server (SQL Express/LocalDB available per environment notes)
            string serverConn = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

            await using Microsoft.Data.SqlClient.SqlConnection _tmpConn = new Microsoft.Data.SqlClient.SqlConnection(serverConn);
            try
            {
                try
                {
                    await _tmpConn.OpenAsync();
                    await _tmpConn.CloseAsync();
                }
                catch (Exception ex)
                {
                    Assert.Inconclusive($"LocalDB not available: {ex.Message}");
                    return;
                }

                string dbName = "MyndSprout_Test_" + Guid.NewGuid().ToString("N");

                try
                {
                    // Create DB using the project helper
                    await SqlService.CreateDatabaseAsync(serverConn, dbName);

                    // Connect to the created DB
                    var connWithDb = $@"Server=(localdb)\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True";
                    var svc = new SqlService();
                    await svc.ConnectAsync(connWithDb);

                    // Exercise GetSchemaAsync
                    var schema = await svc.GetSchemaAsync();
                    Assert.IsNotNull(schema);

                    string[] expectedKeys = new[]
                    {
                        "Tables","Columns","PrimaryKeys","ForeignKeys",
                        "Indexes","Views","Procedures","Functions"
                    };

                    foreach (var k in expectedKeys)
                    {
                        Assert.IsTrue(schema.ContainsKey(k), $"Schema missing expected section: {k}");
                    }
                }
                finally
                {
                    // Best-effort cleanup; surface failures as test failures so humans can inspect the output
                    try
                    {
                        await SqlService.DropDatabaseAsync(serverConn, dbName);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Cleanup DropDatabaseAsync failed for {dbName}: {ex.Message}");
                    }
                }
            }
            finally
            {
            }
        }
    }
}
