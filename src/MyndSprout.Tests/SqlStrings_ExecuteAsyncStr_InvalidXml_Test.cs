//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class SqlStrings_ExecuteAsyncStr_InvalidXml_Test
    {
        [TestMethod]
        public void ExecuteAsyncStr_invalidXml_returnsInvalid()
        {
            var s = new MyndSprout.SqlStrings();
            var outp = s.ExecuteAsyncStr("not xml").GetAwaiter().GetResult();
            Assert.IsTrue(outp.Contains("Invalid input XML") || outp.Contains("success=\"false\""));
        }
    }
}
