//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlConnectionFactory_CreateMaster_UsesIntegratedSecurity_ForTrustedAuth
    {
        [TestMethod]
        public void TestMethod()
        {
            var o = new HardenerOptions { Server = "server", Auth = "Trusted" };
            using var conn = SqlConnectionFactory.CreateMaster(o);
            var csb = new SqlConnectionStringBuilder(conn.ConnectionString);
            Assert.AreEqual("master", csb.InitialCatalog);
            Assert.IsTrue(csb.IntegratedSecurity);
            Assert.AreEqual("server", csb.DataSource);
        }
    }
}
