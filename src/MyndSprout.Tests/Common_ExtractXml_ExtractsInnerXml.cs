//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public class Common_ExtractXml_ExtractsInnerXml
    {
        [TestMethod]
        public void ExtractXml_ReturnsInnerXml_WhenInputContainsXml()
        {
            string input = "prefix <?xml version=\"1.0\"?><Root><Child>value</Child></Root> suffix";
            string? actual = Common.ExtractXml(input);
            Assert.IsNotNull(actual);
            Assert.IsTrue(actual!.Contains("<Root"));
            Assert.IsTrue(actual.Contains("<Child>value</Child>"));
        }
    }
}

