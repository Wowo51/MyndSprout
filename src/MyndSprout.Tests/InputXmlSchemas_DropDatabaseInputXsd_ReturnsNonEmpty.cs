//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class InputXmlSchemas_DropDatabaseInputXsd_ReturnsNonEmpty
    {
        [TestMethod]
        public void DropDatabaseInputXsd_ReturnsNonEmpty()
        {
            var xsd = InputXmlSchemas.DropDatabaseInputXsd();
            Assert.IsFalse(string.IsNullOrWhiteSpace(xsd));
            Assert.IsTrue(xsd.Contains("DropDatabase") || xsd.Contains("databaseName") || xsd.Contains("drop"));
        }
    }
}

