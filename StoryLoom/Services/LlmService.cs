using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace StoryLoom.Services
{
    /// <summary>
    /// 大语言模型 (LLM) 服务类。
    /// 负责编排业务逻辑，使用 PromptTemplates 构建提示词，并通过 LlmClient 与模型交互。
    /// </summary>
    public class LlmService
    {
        private readonly LlmClient _llmClient;
        private readonly SettingsService _settings;
        private readonly LogService _logger;

        public LlmService(LlmClient llmClient, SettingsService settings, LogService logger)
        {
            _llmClient = llmClient;
            _settings = settings;
            _logger = logger;
        }

        public async Task<string> TestConnectionAsync()
        {
            _logger.Log($"Testing connection to {_settings.ApiUrl} with model {_settings.ModelName}...");
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = PromptTemplates.TestConnection }
            };

            var response = await _llmClient.GetCompletionAsync(messages, 0.7, 50);
            _logger.Log("TestConnection successful.");
            return response;
        }

        public async Task<string> SummarizeTextAsync(string textToSummarize, string existingSummary = "")
        {
             _logger.Log($"Summarizing text (Length: {textToSummarize.Length})...");
             
            var prompt = PromptTemplates.Summarize(textToSummarize, existingSummary);
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = prompt }
            };

            try
            {
                var summary = await _llmClient.GetCompletionAsync(messages, 0.5, 500);
                _logger.Log($"Text summarized successfully (Length: {summary.Length}).");
                return summary;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "SummarizeTextAsync");
                 return existingSummary; // Fallback
            }
        }

        public async Task<string> EnhanceTextAsync(string input, string type)
        {
             _logger.Log($"Enhancing text [{type}]...");
             
             var prompt = PromptTemplates.Enhance(input, type);
             var messages = new List<ChatMessage>
             {
                 new ChatMessage { Role = "user", Content = prompt }
             };

             var enhanced = await _llmClient.GetCompletionAsync(messages, 0.8, 1000);
             _logger.Log($"Text enhanced successfully (Length: {enhanced.Length}).");
             return enhanced;
        }

        public async Task<List<string>> GetSuggestionsAsync(List<ChatMessage> contextMessages, string? actionType = null)
        {
            _logger.Log($"Getting suggestions based on {contextMessages.Count} messages...");

            // Clone context and add specific instruction
            var messages = new List<ChatMessage>(contextMessages);
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = PromptTemplates.GetSuggestions(actionType)
            });

            try
            {
                var content = await _llmClient.GetCompletionAsync(messages, 0.9, 300);
                
                // Content *should* be a JSON array string as per new prompts.
                // Try to parse it directly.
                try 
                {
                    // Clean up potential markdown code blocks if the LLM adds them despite instructions
                    var cleanContent = content;
                    if (cleanContent.StartsWith("```json")) 
                    {
                        cleanContent = cleanContent.Replace("```json", "").Replace("```", "");
                    }
                    else if (cleanContent.StartsWith("```"))
                    {
                        cleanContent = cleanContent.Replace("```", "");
                    }
                    
                    var suggestions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(cleanContent.Trim());
                    return suggestions ?? new List<string>();
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, $"GetSuggestionsAsync JSON Parse Error. Content: {content}");
                    // Fallback to line splitting if JSON fails? Or just return raw content wrapped?
                    // Let's return the raw content if it's short enough, or a generic error.
                    // Actually, if it fails, it might be plain text.
                    return new List<string> { content };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSuggestionsAsync");
                return new List<string> { "Error fetching suggestions." };
            }
        }

        /// <summary>
        /// Starts the story generation process.
        /// Constructs the system prompt using templates and streams the response.
        /// </summary>
        public IAsyncEnumerable<string> StartGenerateAsync(List<ChatMessage> historyMessages, string background, string protagonist, string summary, string? actionType = null)
        {
            _logger.Log($"[{nameof(LlmService)}] {nameof(StartGenerateAsync)} called");
            // unique service method for "Action" logic (Story Generation)
            
            // 1. Construct System Prompt
            var systemPrompt = PromptTemplates.StoryGenerationSystemPrompt(background, protagonist, summary, actionType);
            
            // 2. Build full context
            var fullContext = new List<ChatMessage>();
            fullContext.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            fullContext.AddRange(historyMessages);

            // 3. Stream
            return _llmClient.StreamCompletionAsync(fullContext, _settings.Temperature, _settings.MaxContextWindow);
        }
    }

    // Keeping ChatMessage/ChatRequest definition here or move to LlmClient if shared?
    // They are used in ConversationService too. 
    // Ideally they should be in a shared model file, but for now I'll leave ChatMessage here (or duplicate/move).
    // ChatRequest is internal to LlmClient now (anonymous type used there), so we only need ChatMessage.
    
    public class ChatMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }
}
