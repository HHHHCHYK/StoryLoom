namespace StoryLoom.Data.Models;

[Serializable]
public class StoryMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public StoryMemoryType Type { get; set; } = StoryMemoryType.Fact;
    public string Content { get; set; } = string.Empty;
    public List<string> EntityRefs { get; set; } = [];
    public List<string> SourceNodeIds { get; set; } = [];
    public double Importance { get; set; }
    public double Confidence { get; set; } = 1;
    public StoryMemoryStatus Status { get; set; } = StoryMemoryStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public int Version { get; set; } = 1;
}

[Serializable]
public class TimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SceneId { get; set; } = string.Empty;
    public List<string> EntityRefs { get; set; } = [];
    public List<string> SourceNodeIds { get; set; } = [];
    public int Order { get; set; }
    public string InWorldTime { get; set; } = string.Empty;
    public double Importance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum StoryMemoryType
{
    Fact,
    Event,
    Relationship,
    Goal,
    Conflict,
    Foreshadowing,
    WorldRule,
    CharacterState,
    LocationState,
    ItemState
}

public enum StoryMemoryStatus
{
    Active,
    Superseded,
    Contradicted,
    Archived
}
