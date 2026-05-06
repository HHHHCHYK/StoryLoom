namespace StoryLoom.Data.Models;

[Serializable]
public class NarrativeDrive
{
    public List<NarrativeActorDrive> ActorDrives { get; set; } = [];
    public List<NarrativeDriveRelation> Relations { get; set; } = [];
    public List<NarrativeOpportunity> Opportunities { get; set; } = [];
    public List<NarrativeConflict> Conflicts { get; set; } = [];
    public List<NarrativeConstraint> Constraints { get; set; } = [];
    public List<NarrativeClue> Clues { get; set; } = [];
    public List<NarrativeStake> Stakes { get; set; } = [];
    public List<ProgressMarker> ProgressMarkers { get; set; } = [];
}

[Serializable]
public class NarrativeActorDrive
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public NarrativeActorType ActorType { get; set; } = NarrativeActorType.Character;
    public string DisplayName { get; set; } = string.Empty;
    public StoryEntityBrief Brief { get; set; } = new();
    public StoryEntityDetail Detail { get; set; } = new();
    public StoryEntityState State { get; set; } = new();
    public StoryEntityMetadata Metadata { get; set; } = new();
    public List<StoryGoal> Goals { get; set; } = [];
    public List<NarrativeMotivation> Motivations { get; set; } = [];
    public List<NarrativePressure> Pressures { get; set; } = [];
    public List<NarrativeStrategy> Strategies { get; set; } = [];
    public List<NarrativeResource> Resources { get; set; } = [];
    public List<NarrativeLimit> Limits { get; set; } = [];
    public double Influence { get; set; }
}

[Serializable]
public class NarrativeDriveRelation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceActorDriveId { get; set; } = string.Empty;
    public string TargetActorDriveId { get; set; } = string.Empty;
    public NarrativeDriveRelationType Type { get; set; } = NarrativeDriveRelationType.Neutral;
    public string Description { get; set; } = string.Empty;
    public double Strength { get; set; }
    public bool IsKnownToUser { get; set; }
}

[Serializable]
public class StoryGoal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorDriveId { get; set; } = string.Empty;
    public StoryGoalType Type { get; set; } = StoryGoalType.Personal;
    public StoryGoalStatus Status { get; set; } = StoryGoalStatus.Active;
    public string Title { get; set; } = string.Empty;
    public string DesiredOutcome { get; set; } = string.Empty;
    public string FailureConsequence { get; set; } = string.Empty;
    public double Priority { get; set; }
    public double Progress { get; set; }
    public List<string> RelatedEntityIds { get; set; } = [];
    public List<string> RelatedMemoryIds { get; set; } = [];
}

[Serializable]
public class NarrativeMotivation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorDriveId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NarrativeMotivationType Type { get; set; } = NarrativeMotivationType.Emotional;
    public double Strength { get; set; }
    public bool IsHidden { get; set; }
}

[Serializable]
public class NarrativePressure
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorDriveId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NarrativePressureType Type { get; set; } = NarrativePressureType.Time;
    public double Intensity { get; set; }
    public bool IsEscalating { get; set; }
}

[Serializable]
public class NarrativeStrategy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorDriveId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NarrativeStrategyType Type { get; set; } = NarrativeStrategyType.Direct;
    public double Subtlety { get; set; }
    public bool IsKnownToUser { get; set; }
}

[Serializable]
public class NarrativeResource
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorDriveId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NarrativeResourceType Type { get; set; } = NarrativeResourceType.Knowledge;
    public double Strength { get; set; }
    public bool IsAvailable { get; set; } = true;
}

[Serializable]
public class NarrativeLimit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ActorDriveId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NarrativeLimitType Type { get; set; } = NarrativeLimitType.Physical;
    public double Severity { get; set; }
}

public enum NarrativeActorType
{
    Character,
    Faction,
    Location,
    Item,
    WorldEvent,
    Mystery,
    Curse,
    SystemForce
}

public enum NarrativeDriveRelationType
{
    Supports,
    Blocks,
    Competes,
    Manipulates,
    DependsOn,
    Mirrors,
    Contradicts,
    Reveals,
    Neutral
}

public enum StoryGoalType
{
    Main,
    Personal,
    Hidden,
    Faction,
    Survival,
    Emotional,
    Moral,
    Temporary
}

public enum StoryGoalStatus
{
    Active,
    Suspended,
    Completed,
    Failed,
    Abandoned,
    Hidden
}

public enum NarrativeMotivationType
{
    Emotional,
    Moral,
    Survival,
    Ambition,
    Fear,
    Love,
    Duty,
    Revenge,
    Curiosity,
    Faith,
    Secret
}

public enum NarrativePressureType
{
    Time,
    Threat,
    Resource,
    Social,
    Emotional,
    Moral,
    Mystery,
    Environmental
}

public enum NarrativeStrategyType
{
    Direct,
    Indirect,
    Deceptive,
    Cooperative,
    Protective,
    Aggressive,
    Avoidant,
    Manipulative,
    Sacrificial
}

public enum NarrativeResourceType
{
    Knowledge,
    Item,
    Ally,
    Authority,
    Location,
    Skill,
    Magic,
    Secret,
    Money,
    Reputation
}

public enum NarrativeLimitType
{
    Physical,
    Moral,
    Social,
    Knowledge,
    Resource,
    Time,
    Emotional,
    Magical,
    Political
}
