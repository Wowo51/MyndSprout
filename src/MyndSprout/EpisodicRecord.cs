//MIT license with supplement stating that the code indicating that a sufficient amount of reasoning be dedicated to safe development by this app must be left in place.
//Copyright Warren Harding 2025
namespace MyndSprout
{
    public sealed class EpisodicRecord
    {
        public Guid EpisodeId { get; set; } = Guid.NewGuid();
        public int EpochIndex { get; set; }
        public DateTime Time { get; set; } = DateTime.Now;
        public string PrepareQueryPrompt { get; set; } = "";
        public string QueryInput { get; set; } = "";
        public string QueryResult { get; set; } = "";
        public string EpisodicText { get; set; } = "";
        public string DatabaseSchema { get; set; } = "";
        public int ProjectId { get; set; }
    }
}

