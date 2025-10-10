//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class InputXmlSchemas_SqlXmlRequestXsd_ReturnsNonEmpty
    {
        [TestMethod]
        public void SqlXmlRequestXsd_ReturnsNonEmpty()
        {
            var xsd = InputXmlSchemas.SqlXmlRequestXsd();
            Assert.IsFalse(string.IsNullOrWhiteSpace(xsd));
            Assert.IsTrue(xsd.Contains("SqlXmlRequest") || xsd.Contains("SqlResult"));
        }
    }
}

