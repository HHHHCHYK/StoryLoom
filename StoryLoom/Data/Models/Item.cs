namespace StoryLoom.Data.Models;

[Serializable]
public class Item:ISavable
{
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Description { get; set; } = [];
    public Character? Holder {get; set;}
}

public interface IItemHolder
{
    List<Item> Items { get; }
}