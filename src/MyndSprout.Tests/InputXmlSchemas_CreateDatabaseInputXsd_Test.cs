//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class InputXmlSchemas_CreateDatabaseInputXsd_Test
    {
        [TestMethod]
        public void CreateDatabaseInputXsd_contains_name()
        {
            var xsd = InputXmlSchemas.CreateDatabaseInputXsd();
            Assert.IsFalse(string.IsNullOrWhiteSpace(xsd));
            Assert.IsTrue(xsd.Contains("CreateDatabaseInput"));
        }
    }
}
