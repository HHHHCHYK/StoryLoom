namespace StoryLoom.Data.Models;

public interface ISavable
{
    public void Save() => SaveManager.Instance.SingleCapture(this);
}