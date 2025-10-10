//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.SqlClient;

namespace SqlContain.Tests
{
    [TestClass]
    public sealed class SqlConnectionFactory_CreateMaster_UsesCredentials_ForSqlAuth
    {
        [TestMethod]
        public void TestMethod()
        {
            var o = new HardenerOptions { Server = "s", Auth = "Sql", User = "u", Password = "p" };
            using var conn = SqlConnectionFactory.CreateMaster(o);
            var csb = new SqlConnectionStringBuilder(conn.ConnectionString);
            Assert.IsFalse(csb.IntegratedSecurity);
            Assert.AreEqual("u", csb.UserID);
            Assert.AreEqual("p", csb.Password);
        }
    }
}
