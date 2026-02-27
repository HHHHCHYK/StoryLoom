namespace StoryLoom.Data.Models;

[Serializable]
public class Character : Faction, IItemHolder,ISavable
{
    public List<string> Origins { get; set; } = [];
    public Scene? CurrentScene { get; set; } = null;
}

public interface ICharacterHolder
{
    List<Character> Characters { get; }
}