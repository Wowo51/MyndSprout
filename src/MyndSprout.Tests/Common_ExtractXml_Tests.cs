//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class Common_ExtractXml_Tests
    {
        [TestMethod]
        public void ExtractXml_ReturnsInnerXml_WhenWrapped()
        {
            var input = "prefix <?xml version=\"1.0\"?><root><a>1</a></root> suffix";
            var xml = Common.ExtractXml(input);
            Assert.IsNotNull(xml);
            StringAssert.StartsWith(xml, "<root");
            StringAssert.Contains(xml, "<a>1</a>");
        }

        [TestMethod]
        public void ExtractXml_ReturnsNull_WhenNoXml()
        {
            var input = "no xml here";
            var xml = Common.ExtractXml(input);
            Assert.IsNull(xml);
        }
    }
}
