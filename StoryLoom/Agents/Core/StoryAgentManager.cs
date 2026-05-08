using StoryLoom.Services;

namespace StoryLoom.Agents.Core;

public class StoryAgentManager
{
    private readonly IReadOnlyList<IStoryAgent> _agents;
    private readonly LogService _logger;

    public StoryAgentManager(IEnumerable<IStoryAgent> agents, LogService logger)
    {
        _agents = agents.ToList();
        _logger = logger;
    }

    public IReadOnlyList<IStoryAgent> ListAgents()
    {
        return _agents;
    }

    public async Task<StoryAgentResult> RunAsync(StoryAgentType type, StoryAgentContext? context = null, StoryAgentRunOptions? options = null)
    {
        var agent = _agents.FirstOrDefault(item => item.Type == type);
        if (agent == null)
        {
            return new StoryAgentResult
            {
                AgentType = type,
                AgentName = type.ToString(),
                Success = false,
                ErrorMessage = "Agent is not registered."
            };
        }

        var request = new StoryAgentRequest
        {
            AgentType = type,
            Context = context ?? new StoryAgentContext(),
            Options = options ?? new StoryAgentRunOptions()
        };

        return await RunAgentSafelyAsync(agent, request);
    }

    public async Task<IReadOnlyList<StoryAgentResult>> RunAllAsync(StoryAgentContext? context = null, StoryAgentRunOptions? options = null)
    {
        var results = new List<StoryAgentResult>();
        foreach (var agent in _agents)
        {
            var request = new StoryAgentRequest
            {
                AgentType = agent.Type,
                Context = context ?? new StoryAgentContext(),
                Options = options ?? new StoryAgentRunOptions()
            };

            results.Add(await RunAgentSafelyAsync(agent, request));
        }

        return results;
    }

    private async Task<StoryAgentResult> RunAgentSafelyAsync(IStoryAgent agent, StoryAgentRequest request)
    {
        var startedAt = DateTime.UtcNow;
        try
        {
            _logger.Log($"Story agent started: {agent.Name}");
            var result = await agent.RunAsync(request);
            result.AgentType = agent.Type;
            result.AgentName = agent.Name;
            result.Duration = DateTime.UtcNow - startedAt;
            _logger.Log($"Story agent finished: {agent.Name}, Success: {result.Success}, Duration: {result.Duration.TotalMilliseconds:F0}ms");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Story agent failed: {agent.Name}");
            return new StoryAgentResult
            {
                AgentType = agent.Type,
                AgentName = agent.Name,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startedAt
            };
        }
    }
}
