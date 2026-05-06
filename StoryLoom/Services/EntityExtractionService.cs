using System.Text.Json;
using StoryLoom.Data.Models;

namespace StoryLoom.Services;

public class EntityExtractionService
{
    private readonly LlmClient _llmClient;
    private readonly SettingsService _settings;
    private readonly LogService _logger;

    public EntityExtractionService(LlmClient llmClient, SettingsService settings, LogService logger)
    {
        _llmClient = llmClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<EntityExtractionResult> ExtractAsync(string text)
    {
        if (!_settings.IsEntityParserEnabled || !_settings.IsEntityParserConfigured || string.IsNullOrWhiteSpace(text))
        {
            return new EntityExtractionResult();
        }

        var messages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "system",
                Content = "你是 StoryLoom 的世界实体解析器。你只输出 JSON，不输出 Markdown，不输出解释。"
            },
            new ChatMessage
            {
                Role = "user",
                Content = BuildPrompt(text)
            }
        };

        try
        {
            var response = await _llmClient.GetCompletionAsync(messages, 0.1, 1200, LlmModelRole.EntityParser);
            var json = ExtractJson(response);
            var result = JsonSerializer.Deserialize<EntityExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Normalize(result ?? new EntityExtractionResult());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EntityExtractionService.ExtractAsync");
            return new EntityExtractionResult();
        }
    }

    private static string BuildPrompt(string text)
    {
        return $$"""
请从下面的故事对话中提取可以进入世界构建页的实体。

实体类型：
- characters：角色、人物、具备行动能力的个体
- factions：组织、势力、族群、机构
- items：物品、道具、武器、线索物、重要概念物
- scenes：地点、场景、区域、建筑

要求：
- 只提取文本中已经出现或明确暗示的实体。
- 不要虚构实体。
- 如果信息不足，summary 可以简短。
- descriptions 用于保存具体细节。
- 输出必须是合法 JSON。
- 不要输出 Markdown 代码块。

JSON 格式：
{
  "characters": [
    { "name": "", "summary": "", "descriptions": [] }
  ],
  "factions": [
    { "name": "", "summary": "", "descriptions": [] }
  ],
  "items": [
    { "name": "", "summary": "", "descriptions": [] }
  ],
  "scenes": [
    { "name": "", "summary": "", "descriptions": [] }
  ]
}

待解析文本：
{{text}}
""";
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..].Trim();
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed[3..].Trim();
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3].Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }

    private static EntityExtractionResult Normalize(EntityExtractionResult result)
    {
        result.Characters = NormalizeEntities(result.Characters);
        result.Factions = NormalizeEntities(result.Factions);
        result.Items = NormalizeEntities(result.Items);
        result.Scenes = NormalizeEntities(result.Scenes);
        return result;
    }

    private static List<ExtractedEntity> NormalizeEntities(IEnumerable<ExtractedEntity> entities)
    {
        return entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Name))
            .Select(entity => new ExtractedEntity
            {
                Name = entity.Name.Trim(),
                Summary = entity.Summary.Trim(),
                Descriptions = entity.Descriptions
                    .Where(description => !string.IsNullOrWhiteSpace(description))
                    .Select(description => description.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .GroupBy(entity => entity.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExtractedEntity
            {
                Name = group.First().Name,
                Summary = group.FirstOrDefault(entity => !string.IsNullOrWhiteSpace(entity.Summary))?.Summary ?? string.Empty,
                Descriptions = group.SelectMany(entity => entity.Descriptions).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();
    }
}
