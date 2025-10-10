//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using System;

namespace MyndSprout.Tests
{
    [TestClass]
    public class AgentFactory_CreateAgent_NullSqlStrings_ThrowsArgumentNull
    {
        [TestMethod]
        public void CreateAgent_NullSqlStrings_ThrowsArgumentNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => AgentFactory.CreateAgent((SqlStrings)null!, 3));
        }
    }
}

