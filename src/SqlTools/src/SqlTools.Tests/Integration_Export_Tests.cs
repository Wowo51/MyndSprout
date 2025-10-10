//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using SqlTools;

namespace SqlTools.Tests
{
    [TestClass]
    public class Integration_Export_Tests
    {
        [TestMethod]
        public async Task ExportMethods_DoNotThrow_OnEmptyDb()
        {
            var dbName = "SqlToolsTests_Export_" + Guid.NewGuid().ToString("N");
            try
            {
                DbTestHelpers.CreateDatabase(dbName);
                using var conn = new SqlConnection(DbTestHelpers.GetTestDbConnectionString(dbName));
                await conn.OpenAsync();

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "IF OBJECT_ID(N'dbo.Files', N'U') IS NULL CREATE TABLE dbo.Files (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(255) NOT NULL, Content NVARCHAR(MAX) NULL);";
                cmd.ExecuteNonQuery();

                // Call the export methods to ensure they execute without throwing exceptions.
                ExportSchema.Export(conn);
                ExportDataXml.Export(conn);

                // If methods return values, this still verifies they completed. No further assertions to avoid depending on return types.
                Assert.IsTrue(true);
            }
            finally
            {
                try { DbTestHelpers.DropDatabase(dbName); } catch { }
            }
        }
    }
}
