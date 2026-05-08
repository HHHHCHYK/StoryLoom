using System.Net.Http;
using Microsoft.SemanticKernel;
using StoryLoom.Services;

namespace StoryLoom.Agents.SemanticKernel;

public class SemanticKernelAgentFactory
{
    private const string AgentServiceId = "storyloom-agent";

    private readonly SettingsService _settings;
    private readonly IHttpClientFactory _httpClientFactory;

    public SemanticKernelAgentFactory(SettingsService settings, IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
    }

    public Kernel CreatePromptKernel()
    {
        var modelName = _settings.PromptModelName.Trim();
        var apiUrl = _settings.PromptApiUrl.Trim();
        var apiKey = _settings.PromptApiKey.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = _settings.ApiKey.Trim();
        }

        if (string.IsNullOrWhiteSpace(modelName) ||
            string.IsNullOrWhiteSpace(apiUrl) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("提示模型配置不完整，无法运行 Agent 测试。");
        }

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: modelName,
            endpoint: new Uri(apiUrl),
            apiKey: apiKey,
            orgId: null,
            serviceId: AgentServiceId,
            httpClient: _httpClientFactory.CreateClient());

        return builder.Build();
    }
}
