namespace StoryLoom.Agents.Core;

public class StoryAgentRunOptions
{
    public bool DryRun { get; set; } = true;
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
}
