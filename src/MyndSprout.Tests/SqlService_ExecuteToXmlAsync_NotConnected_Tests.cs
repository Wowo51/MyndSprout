//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using System;
using System.Threading.Tasks;

[TestClass]
public class SqlService_ExecuteToXmlAsync_NotConnected_Tests
{
    [TestMethod]
    public async Task ExecuteToXmlAsync_WithoutDatabase_ThrowsInvalidOperation()
    {
        SqlService svc = new SqlService();
        SqlXmlRequest req = new SqlXmlRequest { Sql = "SELECT 1" };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () => await svc.ExecuteToXmlAsync(req));
    }
}

