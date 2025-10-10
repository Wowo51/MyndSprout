//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class InputXmlSchemas_CreateDatabaseInputXsd_ReturnsXsd
    {
        [TestMethod]
        public void CreateDatabaseInputXsd_ReturnsNonEmptyContainingRootName()
        {
            string xsd = InputXmlSchemas.CreateDatabaseInputXsd();
            Assert.IsFalse(string.IsNullOrEmpty(xsd));
            Assert.IsTrue(xsd.IndexOf("CreateDatabaseInput", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

