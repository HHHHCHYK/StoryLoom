namespace StoryLoom.Data.Models;

[Serializable]
public class NarrativeOpportunity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelatedGoalId { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public List<string> RelatedEntityIds { get; set; } = [];
    public List<string> RelatedMemoryIds { get; set; } = [];
    public NarrativeOpportunityStatus Status { get; set; } = NarrativeOpportunityStatus.Available;
    public double Urgency { get; set; }
    public double Reliability { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiresAt { get; set; }
}

[Serializable]
public class NarrativeConflict
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string RelatedGoalId { get; set; } = string.Empty;
    public NarrativeConflictType Type { get; set; } = NarrativeConflictType.External;
    public NarrativeConflictStatus Status { get; set; } = NarrativeConflictStatus.Active;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> InvolvedEntityIds { get; set; } = [];
    public double Intensity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Serializable]
public class NarrativeConstraint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NarrativeConstraintType Type { get; set; } = NarrativeConstraintType.Soft;
    public double Strength { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> RelatedGoalIds { get; set; } = [];
    public List<string> RelatedEntityIds { get; set; } = [];
}

[Serializable]
public class NarrativeClue
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string RelatedGoalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public NarrativeClueStatus Status { get; set; } = NarrativeClueStatus.Unknown;
    public double Reliability { get; set; } = 1;
    public List<string> SourceNodeIds { get; set; } = [];
}

[Serializable]
public class NarrativeStake
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string RelatedGoalId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Severity { get; set; }
    public NarrativeStakeStatus Status { get; set; } = NarrativeStakeStatus.Active;
}

[Serializable]
public class ProgressMarker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string RelatedGoalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProgressMarkerStatus Status { get; set; } = ProgressMarkerStatus.Pending;
    public int Order { get; set; }
    public bool IsRequired { get; set; }
}

public enum NarrativeOpportunityStatus
{
    Available,
    Chosen,
    Ignored,
    Expired,
    Resolved
}

public enum NarrativeConflictType
{
    Internal,
    External,
    Social,
    Moral,
    Environmental,
    Informational,
    Temporal
}

public enum NarrativeConflictStatus
{
    Active,
    Escalated,
    Resolved,
    Dormant
}

public enum NarrativeConstraintType
{
    Hard,
    Soft,
    Style,
    Pacing,
    WorldRule,
    CharacterBehavior
}

public enum NarrativeClueStatus
{
    Unknown,
    Discovered,
    Verified,
    False,
    Misleading,
    Consumed
}

public enum NarrativeStakeStatus
{
    Active,
    Increased,
    Reduced,
    Resolved
}

public enum ProgressMarkerStatus
{
    Pending,
    Available,
    Completed,
    Skipped,
    Failed
}
