//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MyndSprout;

namespace MyndSprout.Tests
{
    [TestClass]
    public class Common_FromXmlOnly_EpisodicRecord_Deserializes
    {
        [TestMethod]
        public void FromXmlOnly_MinimalEpisodicRecord_Parses()
        {
            string guid = Guid.NewGuid().ToString();
            string xml = $"<EpisodicRecord><EpisodeId>{guid}</EpisodeId><EpochIndex>2</EpochIndex></EpisodicRecord>";
            var rec = Common.FromXmlOnly<EpisodicRecord>(xml);
            Assert.IsNotNull(rec);
            Assert.AreEqual(2, rec!.EpochIndex);
            Assert.AreEqual(Guid.Parse(guid), rec.EpisodeId);
        }
    }
}

