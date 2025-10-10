//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class AgentFactory_CreateAgent_NullSqlStrings_Throws
    {
        [TestMethod]
        public void CreateAgent_NullSqlStrings_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => AgentFactory.CreateAgent((SqlStrings)null!, 1));
        }
    }
}

