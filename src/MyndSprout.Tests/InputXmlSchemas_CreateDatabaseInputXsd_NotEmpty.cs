//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

[TestClass]
public class InputXmlSchemas_CreateDatabaseInputXsd_NotEmpty
{
    [TestMethod]
    public void CreateDatabaseInputXsd_ReturnsNonEmpty()
    {
        var s = InputXmlSchemas.CreateDatabaseInputXsd();
        Assert.IsFalse(string.IsNullOrWhiteSpace(s));
    }
}

