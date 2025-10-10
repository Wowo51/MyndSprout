//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public class AgentFactory_CreateAgent_WithSqlStrings_ReturnsAgent
    {
        [TestMethod]
        public void CreateAgent_NullSqlStrings_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => AgentFactory.CreateAgent(null!));
        }

        [TestMethod]
        public void CreateAgent_ValidSqlStrings_ReturnsSqlAgent()
        {
            var sql = new SqlStrings();
            var agent = AgentFactory.CreateAgent(sql);
            Assert.IsNotNull(agent);
        }
    }
}

