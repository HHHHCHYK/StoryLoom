namespace StoryLoom.Data.Models;

[Serializable]
public class StoryEntityBrief
{
    public string OneLine { get; set; } = string.Empty;
    public string ShortSummary { get; set; } = string.Empty;
    public List<string> KeyFacts { get; set; } = [];
    public List<string> ActiveTags { get; set; } = [];
}

[Serializable]
public class StoryEntityDetail
{
    public string FullDescription { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public string Appearance { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string History { get; set; } = string.Empty;
    public List<string> Secrets { get; set; } = [];
    public List<string> Notes { get; set; } = [];
}

[Serializable]
public class StoryEntityState
{
    public string CurrentLocationId { get; set; } = string.Empty;
    public string CurrentMood { get; set; } = string.Empty;
    public string CurrentGoal { get; set; } = string.Empty;
    public string CurrentConflict { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public List<string> ActiveMemoryIds { get; set; } = [];
    public List<string> ActiveGoalIds { get; set; } = [];
    public List<string> ActiveFlags { get; set; } = [];
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

[Serializable]
public class StoryEntityMetadata
{
    public double Importance { get; set; }
    public double Relevance { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsVisibleToUser { get; set; } = true;
    public bool HasHiddenInformation { get; set; }
    public StoryEntityVisibility Visibility { get; set; } = StoryEntityVisibility.Public;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum StoryEntityVisibility
{
    Public,
    HiddenFromUser,
    LockedUntilTriggered,
    InternalOnly
}

public enum ContextDepth
{
    Brief,
    Standard,
    Detailed,
    Full
}

public enum GenerationMode
{
    Lightweight,
    Standard,
    Deep
}
