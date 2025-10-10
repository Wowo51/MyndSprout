//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

[TestClass]
public class Common_ToXmlSchema_Generic_ReturnsString
{
    [TestMethod]
    public void ToXmlSchema_Generic_ReturnsNonEmpty()
    {
        string xml = InputXmlSchemas.EmptyXsd();
        Assert.IsFalse(string.IsNullOrWhiteSpace(xml));
    }
}

