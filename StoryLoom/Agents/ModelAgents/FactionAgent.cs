using Microsoft.SemanticKernel.Agents;
using StoryLoom.Data.Models;

namespace StoryLoom.Agents.ModelAgents;

public class FactionAgent(Faction faction,ChatCompletionAgent chatAgent) : IAgent
{
    
    // Base Setting
    public Faction FactionData { get; private set; } = faction;
    
    // Core Agent
    private ChatCompletionAgent ChatCompletionAgent = chatAgent;

    public Task TriggerDecision()
    {
        throw new NotImplementedException();
    }
}