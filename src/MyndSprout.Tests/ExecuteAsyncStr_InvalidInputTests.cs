//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace MyndSprout.Tests
{
    [TestClass]
    public class ExecuteAsyncStr_InvalidInputTests
    {
        [TestMethod]
        public async Task ExecuteAsyncStr_ReturnsInputError_OnMalformedXml()
        {
            var sql = new SqlStrings();
            string result = await sql.ExecuteAsyncStr("<notvalid");
            Assert.IsFalse(string.IsNullOrWhiteSpace(result));
            StringAssert.Contains(result, "<Type>Input</Type>");
            StringAssert.Contains(result, "Invalid input XML");
        }
    }
}

