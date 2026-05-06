namespace StoryLoom.Data.Models;

[Serializable]
public class GenerationContext
{
    public string ProjectId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public StoryWorldSnapshot WorldSnapshot { get; set; } = new();
    public NarrativeDrive ActiveDrive { get; set; } = new();
    public List<StoryNode> RecentNodes { get; set; } = [];
    public List<StoryMemory> RelevantMemories { get; set; } = [];
    public List<TimelineEvent> RelevantTimelineEvents { get; set; } = [];
    public string UserInstruction { get; set; } = string.Empty;
    public List<NarrativeConstraint> Constraints { get; set; } = [];
    public GenerationMode Mode { get; set; } = GenerationMode.Lightweight;
    public ContextDepth Depth { get; set; } = ContextDepth.Brief;
}

[Serializable]
public class StoryWorldSnapshot
{
    public string Background { get; set; } = string.Empty;
    public string Protagonist { get; set; } = string.Empty;
    public string CurrentSceneId { get; set; } = string.Empty;
    public string CurrentSceneSummary { get; set; } = string.Empty;
    public List<StoryEntityBrief> RelevantEntities { get; set; } = [];
    public List<string> ActiveWorldRules { get; set; } = [];
}
