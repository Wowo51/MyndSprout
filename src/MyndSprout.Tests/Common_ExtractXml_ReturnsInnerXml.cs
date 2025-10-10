//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class Common_ExtractXml_ReturnsInnerXml
    {
        [TestMethod]
        public void ExtractXml_HappyPath_ReturnsInnerXml()
        {
            string input = "prefix <?xml version=\"1.0\"?> <Root><Child>val</Child></Root> suffix";
            var result = Common.ExtractXml(input);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("<Root"));
            Assert.IsTrue(result.Contains("<Child>val</Child>"));
        }

        [TestMethod]
        public void ExtractXml_NoXml_ReturnsNull()
        {
            string input = "no xml here";
            var result = Common.ExtractXml(input);
            Assert.IsNull(result);
        }
    }
}
