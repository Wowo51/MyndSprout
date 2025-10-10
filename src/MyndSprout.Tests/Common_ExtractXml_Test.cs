//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class Common_ExtractXml_Test
    {
        [TestMethod]
        public void ExtractsXmlAndReturnsNullWhenNone()
        {
            var xml = Common.ExtractXml("prefix <?xml version='1.0'?><r><a>1</a></r> suffix");
            Assert.IsNotNull(xml);
            Assert.IsTrue(xml.Contains("<r>"));

            var none = Common.ExtractXml("no xml here");
            Assert.IsNull(none);
        }
    }
}
