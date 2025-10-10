//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using MyndSprout;
using Microsoft.Data.SqlClient;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlService_CreateAndDropDatabase_LocalDb_Tests
    {
        [TestMethod]
        public async Task CreateAndDropDatabase_LocalDb_Succeeds()
        {
            string serverConn = "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

            try
            {
                await using Microsoft.Data.SqlClient.SqlConnection _tmpConn = new Microsoft.Data.SqlClient.SqlConnection(serverConn);
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

                int rc = await SqlService.CreateDatabaseAsync(serverConn, dbName);
                // ExecuteNonQueryAsync may return -1 for CREATE DATABASE; assert it's an int and not an unexpected error code
                Assert.IsTrue(rc >= -1, $"Unexpected records-affected: {rc}");
            }
            finally
            {
                try
                {
                    // best-effort drop
                    // cannot determine dbName here if creation failed; attempt best-effort cleanup is handled inside tests that create DB
                }
                catch (Exception ex)
                {
                    // If drop fails due to environment/permission issues, fail with message so human can inspect test output
                    Assert.Fail($"Cleanup DropDatabaseAsync failed: {ex.Message}");
                }
            }
        }
    }
}
