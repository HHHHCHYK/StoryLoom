using StoryLoom.Agents.Factories;
using StoryLoom.Agents.ModelAgents;

namespace StoryLoom.Agents;

public class AgentsManager
{
    public static AgentsManager Instance { get; } = new AgentsManager();
    private List<IAgent> _agents = new();

    public AgentsManager()
    {
        
    }
}