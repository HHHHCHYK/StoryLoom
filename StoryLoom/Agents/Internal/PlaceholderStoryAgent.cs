using StoryLoom.Agents.Core;

namespace StoryLoom.Agents.Internal;

public abstract class PlaceholderStoryAgent : IStoryAgent
{
    public abstract StoryAgentType Type { get; }
    public abstract string Name { get; }
    protected abstract string Responsibility { get; }

    public Task<StoryAgentResult> RunAsync(StoryAgentRequest request)
    {
        return Task.FromResult(new StoryAgentResult
        {
            AgentType = Type,
            AgentName = Name,
            Success = true,
            Summary = $"{Name} is registered and ready. {Responsibility}",
            RawOutput = "Placeholder agent completed a dry-run health check.",
            Data =
            {
                ["runId"] = request.Context.RunId,
                ["dryRun"] = request.Options.DryRun,
                ["responsibility"] = Responsibility
            }
        });
    }
}
