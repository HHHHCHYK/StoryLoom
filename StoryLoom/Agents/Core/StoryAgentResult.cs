namespace StoryLoom.Agents.Core;

public class StoryAgentResult
{
    public StoryAgentType AgentType { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string RawOutput { get; set; } = string.Empty;
    public List<StoryAgentFinding> Findings { get; set; } = [];
    public Dictionary<string, object> Data { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}
