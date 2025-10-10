//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class InputXmlSchemas_SqlXmlRequestXsd_ReturnsXsd
    {
        [TestMethod]
        public void SqlXmlRequestXsd_ReturnsNonEmptyContainingRootName()
        {
            string xsd = InputXmlSchemas.SqlXmlRequestXsd();
            Assert.IsFalse(string.IsNullOrEmpty(xsd));
            Assert.IsTrue(xsd.IndexOf("SqlXmlRequest", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}

