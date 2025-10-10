//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using System;

[TestClass]
public class AgentFactory_CreateAgent_FromSqlStrings_ReturnsSqlAgent
{
    [TestMethod]
    public void CreateAgent_WithNull_ThrowsArgumentNull()
    {
        Assert.ThrowsException<ArgumentNullException>(() => AgentFactory.CreateAgent(null!, 1));
    }

    [TestMethod]
    public void CreateAgent_WithSqlStrings_ReturnsSqlAgent()
    {
        var sql = new SqlStrings();
        var agent = AgentFactory.CreateAgent(sql, 3);
        Assert.IsNotNull(agent);
    }
}

