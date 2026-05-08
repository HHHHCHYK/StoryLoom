using System.Text;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using StoryLoom.Agents.Core;

namespace StoryLoom.Agents.SemanticKernel;

public abstract class SemanticKernelStoryAgent : IStoryAgent
{
    private readonly SemanticKernelAgentFactory _agentFactory;

    protected SemanticKernelStoryAgent(SemanticKernelAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    public abstract StoryAgentType Type { get; }
    public abstract string Name { get; }
    protected abstract string Description { get; }
    protected abstract string Instructions { get; }
    protected abstract string TestInput { get; }

    public async Task<StoryAgentResult> RunAsync(StoryAgentRequest request)
    {
        if (request.Options.DryRun)
        {
            return CreateDryRunResult(request);
        }

        var kernel = _agentFactory.CreatePromptKernel();
        var agent = new ChatCompletionAgent
        {
            Name = Name,
            Description = Description,
            Instructions = Instructions,
            Kernel = kernel
        };

        var history = new ChatHistory();
        history.AddUserMessage(BuildUserMessage(request));

        var output = new StringBuilder();
        await foreach (var message in agent.InvokeAsync(history, cancellationToken: request.Options.CancellationToken))
        {
            output.Append(message.Message.Content);
        }

        var rawOutput = output.ToString().Trim();
        return new StoryAgentResult
        {
            AgentType = Type,
            AgentName = Name,
            Success = true,
            Summary = CreateSummary(rawOutput),
            RawOutput = rawOutput,
            Data =
            {
                ["runId"] = request.Context.RunId,
                ["dryRun"] = false,
                ["description"] = Description
            }
        };
    }

    private StoryAgentResult CreateDryRunResult(StoryAgentRequest request)
    {
        return new StoryAgentResult
        {
            AgentType = Type,
            AgentName = Name,
            Success = true,
            Summary = $"{Name} 已注册。Dry-run 未调用模型。",
            RawOutput = "Dry-run completed without invoking Semantic Kernel.",
            Data =
            {
                ["runId"] = request.Context.RunId,
                ["dryRun"] = true,
                ["description"] = Description
            }
        };
    }

    private string BuildUserMessage(StoryAgentRequest request)
    {
        if (request.Context.Items.TryGetValue("userInput", out var userInput) &&
            userInput is string userInputText &&
            !string.IsNullOrWhiteSpace(userInputText))
        {
            return userInputText;
        }

        return TestInput;
    }

    private static string CreateSummary(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return "Agent 已运行，但模型返回内容为空。";
        }

        return rawOutput.Length <= 160 ? rawOutput : rawOutput[..160] + "...";
    }
}
