//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlStrings_ConnectAsyncStr_BadConnection_Test
    {
        [TestMethod]
        public void ConnectAsyncStr_badConnection_returnsError()
        {
            var s = new MyndSprout.SqlStrings();
            string input = "<ConnectInput><ConnectionString>Data Source=NOPE;Initial Catalog=none;Integrated Security=True</ConnectionString></ConnectInput>";
            var xml = s.ConnectAsyncStr(input).GetAwaiter().GetResult();
            Assert.IsTrue(xml.Contains("success=\"false\"") || xml.Contains("<Error>") || xml.Contains("Invalid"));
        }
    }
}
