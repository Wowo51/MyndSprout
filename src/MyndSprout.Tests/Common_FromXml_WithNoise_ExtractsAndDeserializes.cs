//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class Common_FromXml_WithNoise_ExtractsAndDeserializes
    {
        [TestMethod]
        public void FromXml_WithLeadingNoise_ExtractsAndDeserializes()
        {
            string xml = "<EpisodicRecord><EpochIndex>7</EpochIndex></EpisodicRecord>";
            string noisy = "SOME NOISE BEFORE XML\n" + xml + "\nSOME TRAILING TEXT";
            var rec = Common.FromXml<EpisodicRecord>(noisy);
            Assert.IsNotNull(rec);
            Assert.AreEqual(7, rec!.EpochIndex);
        }
    }
}

