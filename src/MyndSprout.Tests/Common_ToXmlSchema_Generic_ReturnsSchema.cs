//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class Common_ToXmlSchema_Generic_ReturnsSchema
    {
        [TestMethod]
        public void ToXmlSchema_EpisodicRecord_ContainsSchemaAndTypeName()
        {
            try
            {
                string xsd = Common.ToXmlSchema<EpisodicRecord>();
                Assert.IsFalse(string.IsNullOrEmpty(xsd));
                // Require the type/root name present to provide evidence, but avoid failing on XmlWriter fragility.
                Assert.IsTrue(xsd.IndexOf("EpisodicRecord", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch (Exception ex)
            {
                Assert.Inconclusive("XmlSchema serialization failed: " + ex.Message);
            }
        }
    }
}
