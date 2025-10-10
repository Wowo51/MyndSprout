//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class AgentFactory_CreateAgentFromConnectionString_Test
    {
        [TestMethod]
        public async Task CreateAgentFromConnectionString_localdb_master_returnsAgent()
        {
            string conn = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True";
            var agent = await MyndSprout.AgentFactory.CreateAgentFromConnectionStringAsync(conn, 1);
            Assert.IsNotNull(agent);
        }
    }
}
