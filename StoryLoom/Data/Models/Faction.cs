namespace StoryLoom.Data.Models;

[Serializable]
public class Faction : IItemHolder, ICharacterHolder, ISavable
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<Character> Characters { get; set; } = [];
    public List<string> Descriptions { get; set; } = [];
    public List<Item> Items { get; set; } = [];
}