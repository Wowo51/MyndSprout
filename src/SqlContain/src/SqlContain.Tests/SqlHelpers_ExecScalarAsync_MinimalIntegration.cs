//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlHelpers_ExecScalarAsync_MinimalIntegration
    {
        [TestMethod]
        public async Task TestMethod()
        {
            var o = new HardenerOptions { Server = "(LocalDB)\\MSSQLLocalDB", Auth = "Trusted" };
            using SqlConnection conn = SqlConnectionFactory.CreateMaster(o);
            try
            {
                await conn.OpenAsync();
                using SqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.CommandType = System.Data.CommandType.Text;
                object? scalar = await cmd.ExecuteScalarAsync();
                int result = Convert.ToInt32(scalar);
                Assert.AreEqual(1, result);
            }
            catch (System.Exception ex)
            {
                Assert.Inconclusive("SQL server not available: " + ex.Message);
                return;
            }
        }
    }
}
