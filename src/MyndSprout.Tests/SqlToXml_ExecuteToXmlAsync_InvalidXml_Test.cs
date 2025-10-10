//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlToXml_ExecuteToXmlAsync_InvalidXml_Test
    {
        [TestMethod]
        public void ExecuteToXmlAsync_WhenNotConnected_ThrowsInvalidOperation()
        {
            var svc = new SqlService();
            var req = new SqlXmlRequest { Sql = "SELECT 1" };
            Assert.ThrowsException<InvalidOperationException>(() =>
                svc.ExecuteToXmlAsync(req).GetAwaiter().GetResult());
        }
    }
}
