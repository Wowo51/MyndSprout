//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

[TestClass]
public class SqlService_ExecuteToXmlAsync_NullRequest_Tests
{
    [TestMethod]
    public async Task ExecuteToXmlAsync_NullRequest_ThrowsArgumentNull()
    {
        SqlService svc = new SqlService();
        svc.Database = new SqlConnection(); // non-null but not opened; satisfies Database != null check

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await svc.ExecuteToXmlAsync((SqlXmlRequest?)null!));
    }
}

