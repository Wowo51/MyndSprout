//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlService_SaveEpisodicAsync_WithoutConnection_ThrowsInvalidOperation
    {
        [TestMethod]
        public void ExecuteQueryAsync_WithoutConnection_ThrowsInvalidOperation()
        {
            var svc = new SqlService();
            Assert.ThrowsException<InvalidOperationException>(() =>
                svc.ExecuteQueryAsync("SELECT 1").GetAwaiter().GetResult());
        }
    }
}
