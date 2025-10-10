//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class AgentFactory_CreateAgentWithDefaultServer_MethodTests
    {
        [TestMethod]
        public async Task ValidDbNameWithOverride_ReturnsSqlAgent()
        {
            var agent = await AgentFactory.CreateAgentWithDefaultServer("TestDb", 1, @"Server=(localdb)\\MSSQLLocalDB;Database=master;");
            Assert.IsNotNull(agent);
            Assert.IsInstanceOfType(agent, typeof(SqlAgent));
        }
    }
}
