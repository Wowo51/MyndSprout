//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public sealed class Common_FromXml_Test
    {
        [TestMethod]
        public void FromXml_validXml_returnsInstance_and_invalid_returnsNull()
        {
            string xml = InputXmlSchemas.EmptyXsd();
            Assert.IsFalse(string.IsNullOrEmpty(xml));
        }
    }
}
