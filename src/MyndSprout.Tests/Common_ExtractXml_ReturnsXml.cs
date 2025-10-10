//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

[TestClass]
public class Common_ExtractXml_ReturnsXml
{
    [TestMethod]
    public void ExtractsSimpleXmlFromWrappedText()
    {
        string input = "prefix <?xml version=\"1.0\"?><Test>ok</Test> suffix";
        var xml = Common.ExtractXml(input);
        Assert.IsNotNull(xml);
        Assert.IsTrue(xml.Contains("<Test>"));
    }
}

