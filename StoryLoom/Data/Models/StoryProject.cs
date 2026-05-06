namespace StoryLoom.Data.Models;

[Serializable]
public class StoryProject : ISavable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public StoryWorld World { get; set; } = new();
    public List<StoryDocument> Documents { get; set; } = [];
    public List<StoryMemory> Memories { get; set; } = [];
    public List<TimelineEvent> Timeline { get; set; } = [];
    public NarrativeDrive Drive { get; set; } = new();
    public StoryEntityMetadata Metadata { get; set; } = new();
    public int SaveVersion { get; set; } = 2;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

[Serializable]
public class StoryWorld : ICharacterHolder, IItemHolder
{
    public string Background { get; set; } = string.Empty;
    public string Protagonist { get; set; } = string.Empty;
    public List<Character> Characters { get; set; } = [];
    public List<Faction> Factions { get; set; } = [];
    public List<Item> Items { get; set; } = [];
    public List<Scene> Scenes { get; set; } = [];
    public List<string> Rules { get; set; } = [];
}

[Serializable]
public class StoryDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public StoryDocumentType Type { get; set; } = StoryDocumentType.MainStory;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<StoryNode> Nodes { get; set; } = [];
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

[Serializable]
public class StoryNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public StoryNodeRole Role { get; set; } = StoryNodeRole.Narration;
    public string Text { get; set; } = string.Empty;
    public int Position { get; set; }
    public string SceneId { get; set; } = string.Empty;
    public List<string> EntityRefs { get; set; } = [];
    public List<string> MemoryRefs { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public double Importance { get; set; }
    public string Summary { get; set; } = string.Empty;
    public StoryGenerationMetadata GenerationMetadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

[Serializable]
public class StoryGenerationMetadata
{
    public string Model { get; set; } = string.Empty;
    public string PromptTemplate { get; set; } = string.Empty;
    public GenerationMode Mode { get; set; } = GenerationMode.Lightweight;
    public ContextDepth Depth { get; set; } = ContextDepth.Brief;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

public enum StoryDocumentType
{
    MainStory,
    Chapter,
    Outline,
    WorldNote,
    CharacterNote,
    Draft,
    Summary
}

public enum StoryNodeRole
{
    UserInput,
    AssistantOutput,
    Narration,
    Dialogue,
    SystemNote,
    Summary,
    Outline,
    MemoryExtraction
}
