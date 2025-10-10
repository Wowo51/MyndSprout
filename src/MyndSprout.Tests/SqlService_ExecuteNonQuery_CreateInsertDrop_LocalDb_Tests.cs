//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using MyndSprout;
using Microsoft.Data.SqlClient;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlService_ExecuteNonQuery_CreateInsertDrop_LocalDb_Tests
    {
        [TestMethod]
        public async Task ExecuteNonQuery_CreateInsertDrop_LocalDb_Succeeds()
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
                    int rc1 = await SqlService.ExecuteNonQueryAsync(connWithDb, $"CREATE TABLE {tableName} (id INT PRIMARY KEY, val NVARCHAR(100))");
                    Assert.IsTrue(rc1 >= -1, $"CREATE TABLE unexpected rc: {rc1}");
                    int rc2 = await SqlService.ExecuteNonQueryAsync(connWithDb, $"INSERT INTO {tableName} (id, val) VALUES (1, 'x')");
                    Assert.IsTrue(rc2 >= 0, $"INSERT unexpected rc: {rc2}");
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
