namespace StoryLoom.Data.Models;

[Serializable]
public class EntityExtractionResult
{
    public List<ExtractedEntity> Characters { get; set; } = [];
    public List<ExtractedEntity> Factions { get; set; } = [];
    public List<ExtractedEntity> Items { get; set; } = [];
    public List<ExtractedEntity> Scenes { get; set; } = [];
}

[Serializable]
public class ExtractedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Descriptions { get; set; } = [];
}
