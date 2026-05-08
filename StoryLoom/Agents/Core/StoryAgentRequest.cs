namespace StoryLoom.Agents.Core;

public class StoryAgentRequest
{
    public StoryAgentType AgentType { get; set; }
    public StoryAgentContext Context { get; set; } = new();
    public StoryAgentRunOptions Options { get; set; } = new();
}
