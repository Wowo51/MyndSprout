//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using MyndSprout;

[TestClass]
public sealed class SqlStrings_ExecuteToXmlAsyncStr_Tests
{
    [TestMethod]
    public async Task ExecuteToXmlAsyncStr_InvalidXml_ReturnsInvalidInput()
    {
        var sql = new SqlStrings();
        var res = await sql.ExecuteToXmlAsyncStr("not xml");
        Assert.IsTrue(res.Contains("Invalid input XML") || res.ToLowerInvariant().Contains("invalid"));
    }
}

