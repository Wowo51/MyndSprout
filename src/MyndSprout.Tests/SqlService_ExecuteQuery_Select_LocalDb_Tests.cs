//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using MyndSprout;
using Microsoft.Data.SqlClient;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlService_ExecuteQuery_Select_LocalDb_Tests
    {
        [TestMethod]
        public async Task ExecuteQuery_Select_ReturnsRows_LocalDb_Succeeds()
        {
            string serverConn = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

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
                string tableName = "t_" + Guid.NewGuid().ToString("N").Substring(0,8);
                try
                {
                    await SqlService.CreateDatabaseAsync(serverConn, dbName);
                    var connWithDb = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True";
                    await SqlService.ExecuteNonQueryAsync(connWithDb, $"CREATE TABLE {tableName} (id INT PRIMARY KEY, val NVARCHAR(100))");
                    await SqlService.ExecuteNonQueryAsync(connWithDb, $"INSERT INTO {tableName} (id, val) VALUES (1, 'expected')");

                    var svc = new SqlService();
                    await svc.ConnectAsync(connWithDb);
                    var rows = await svc.ExecuteQueryAsync($"SELECT id, val FROM {tableName} WHERE id = 1");

                    Assert.IsNotNull(rows);
                    Assert.IsTrue(rows.Count > 0, "ExecuteQueryAsync returned no rows");

                    bool found = false;
                    foreach (var v in rows[0].Values)
                    {
                        if (v != null && v.ToString()!.Contains("expected"))
                        {
                            found = true;
                            break;
                        }
                    }
                    Assert.IsTrue(found, "Returned result did not contain expected value");
                }
                finally
                {
                    try
                    {
                        var connWithDb = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True";
                        await SqlService.ExecuteNonQueryAsync(connWithDb, $"IF OBJECT_ID('{tableName}','U') IS NOT NULL DROP TABLE {tableName}");
                        await SqlService.DropDatabaseAsync(serverConn, dbName);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Cleanup failed: {ex.Message}");
                    }
                }
            }
            finally
            {
            }
        }
    }
}
