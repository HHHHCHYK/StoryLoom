using StoryLoom.Data.Models;

namespace StoryLoom.Services;

public enum EntityChangeKind
{
    Added,
    Modified
}

public enum ExtractedEntityType
{
    Character,
    Faction,
    Item,
    Scene
}

public enum EntityChangeReviewMode
{
    UserInputPreflight,
    AiResponseReview
}

public class EntityChangePreview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ExtractedEntityType Type { get; set; }
    public EntityChangeKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrentSummary { get; set; } = string.Empty;
    public string IncomingSummary { get; set; } = string.Empty;
    public List<string> CurrentDescriptions { get; set; } = [];
    public List<string> IncomingDescriptions { get; set; } = [];
    public ExtractedEntity Entity { get; set; } = new();
}

public class EntityChangeSet
{
    public EntityChangeReviewMode Mode { get; set; } = EntityChangeReviewMode.AiResponseReview;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<EntityChangePreview> Changes { get; set; } = [];
    public IReadOnlyList<EntityChangePreview> Added => Changes.Where(change => change.Kind == EntityChangeKind.Added).ToList();
    public IReadOnlyList<EntityChangePreview> Modified => Changes.Where(change => change.Kind == EntityChangeKind.Modified).ToList();
    public int Count => Changes.Count;
}
