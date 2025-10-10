//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlConnectionFactory_CreateMaster_InitialCatalog
    {
        [TestMethod]
        public void CreateMaster_SetsInitialCatalog_FromOptions()
        {
            var opts1 = new HardenerOptions { Database = string.Empty };
            using SqlConnection conn1 = SqlConnectionFactory.CreateMaster(opts1);
            var builder1 = new SqlConnectionStringBuilder(conn1.ConnectionString);
            Assert.AreEqual("master", builder1.InitialCatalog);

            var opts2 = new HardenerOptions { Database = "MyDb" };
            using SqlConnection conn2 = SqlConnectionFactory.CreateMaster(opts2);
            var builder2 = new SqlConnectionStringBuilder(conn2.ConnectionString);
            Assert.AreEqual("MyDb", builder2.InitialCatalog);
        }
    }
}
