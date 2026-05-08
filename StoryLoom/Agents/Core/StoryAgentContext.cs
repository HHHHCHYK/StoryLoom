namespace StoryLoom.Agents.Core;

public class StoryAgentContext
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public Dictionary<string, object> Items { get; set; } = [];
}
