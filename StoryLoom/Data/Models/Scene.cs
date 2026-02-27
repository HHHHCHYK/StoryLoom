namespace StoryLoom.Data.Models;

[Serializable]
public class Scene : ICharacterHolder, ISavable
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Scene? Parent { get; set; }
    public List<Character> Characters { get; set; } = [];
    public List<string> Descriptions { get; set; } = [];
}