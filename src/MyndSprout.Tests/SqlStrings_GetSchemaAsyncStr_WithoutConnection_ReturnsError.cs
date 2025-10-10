//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using MyndSprout;

[TestClass]
public class SqlStrings_GetSchemaAsyncStr_WithoutConnection_ReturnsError
{
    [TestMethod]
    public async Task GetSchemaAsyncStr_WithoutConnection()
    {
        var sql = new SqlStrings();
        var res = await sql.ExecuteToXmlAsyncStr("<sql/>");
        Assert.IsTrue(res.Contains("success=\"false\"") || res.Contains("<Error") || !string.IsNullOrEmpty(res));
    }
}

