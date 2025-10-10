//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlTools;

namespace SqlTools.Tests
{
    [TestClass]
    public class FileImporterNullArgsTests
    {
        [TestMethod]
        public async Task EnsureTableAsync_NullConnection_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await FileImporter.EnsureTableAsync(null!));
        }

        [TestMethod]
        public async Task ImportAsync_NullConnection_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await FileImporter.ImportAsync(null!, new[] { new FileImporter.FileRecord() }));
        }

        [TestMethod]
        public async Task ImportAsync_NullFiles_Throws()
        {
            using var conn = new SqlConnection();
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await FileImporter.ImportAsync(conn, null!));
        }
    }
}
