using StoryLoom.Data.Models;

namespace StoryLoom.Data;

public class SaveManager
{
    public static SaveManager Instance { get; } = new SaveManager();

    public void SingleCapture(ISavable savable)
    {
        throw new NotImplementedException();
    }
}