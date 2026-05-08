namespace StoryLoom.Agents.Core;

public interface IStoryAgent
{
    StoryAgentType Type { get; }
    string Name { get; }
    Task<StoryAgentResult> RunAsync(StoryAgentRequest request);
}
