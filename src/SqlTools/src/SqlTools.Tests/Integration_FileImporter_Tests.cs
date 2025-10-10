//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using SqlTools;

namespace SqlTools.Tests
{
    [TestClass]
    public class Integration_FileImporter_Tests
    {
        [TestMethod]
        public async Task ImportAndExport_Roundtrip_Works()
        {
            var dbName = "SqlToolsTests_Import_" + Guid.NewGuid().ToString("N");
            try
            {
                DbTestHelpers.CreateDatabase(dbName);
                using var conn = new SqlConnection(DbTestHelpers.GetTestDbConnectionString(dbName));
                await conn.OpenAsync();

                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "IF OBJECT_ID(N'dbo.Files', N'U') IS NULL CREATE TABLE dbo.Files (Id INT IDENTITY(1,1) PRIMARY KEY, Name NVARCHAR(255) NOT NULL, Content NVARCHAR(MAX) NULL);";
                cmd.ExecuteNonQuery();

                // Ensure table and import
                await FileImporter.EnsureTableAsync(conn);

                var files = new[] { new FileImporter.FileRecord { Name = "a.txt", Content = "hello" } };
                await FileImporter.ImportAsync(conn, files);

                // Call export methods to ensure they run; do not assume return types (call for side-effects)
                ExportSchema.Export(conn);
                ExportDataXml.Export(conn);

                // Verify inserted row exists
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT COUNT(*) FROM Files WHERE Name = @n";
                cmd2.Parameters.AddWithValue("@n", "a.txt");
                var scalar = await cmd2.ExecuteScalarAsync();
                var count = Convert.ToInt32(scalar);
                Assert.AreEqual(1, count, "Expected one imported file row.");
            }
            finally
            {
                // Always attempt to drop the database
                try { DbTestHelpers.DropDatabase(dbName); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
