//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using MyndSprout;

[TestClass]
public class SqlStrings_CreateDatabaseAsyncStr_InvalidXml_ReturnsInvalidInput
{
    [TestMethod]
    public async Task CreateDatabaseAsyncStr_InvalidXml()
    {
        var sql = new SqlStrings();
        var res = await sql.CreateDatabaseAsyncStr("not xml");
        Assert.IsTrue(res.Contains("Invalid input XML"));
    }
}

