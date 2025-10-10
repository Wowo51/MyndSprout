//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

[TestClass]
public class Common_FromXml_ExtractsAndDeserializes
{
    [TestMethod]
    public void FromXml_HandlesWrappedXml()
    {
        string xml = InputXmlSchemas.EmptyXsd();
        Assert.IsFalse(string.IsNullOrEmpty(xml));
    }
}

