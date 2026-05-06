using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace StoryLoom.Services
{
    public enum LlmModelRole
    {
        Story,
        Prompt,
        EntityParser
    }

    public class LlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly SettingsService _settings;
        private readonly LogService _logger;
        private readonly ToastService _toastService;
        private readonly Tokenizer? _tokenizer;

        public LlmClient(HttpClient httpClient, SettingsService settings, LogService logger, ToastService toastService)
        {
            _httpClient = httpClient;
            _settings = settings;
            _logger = logger;
            _toastService = toastService;
            try
            {
                _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Tokenizer");
            }
        }

        public Task<string> GetCompletionAsync(IEnumerable<ChatMessage> messages, double temperature, int maxTokens, bool isPromptModel = false)
        {
            return GetCompletionAsync(messages, temperature, maxTokens, isPromptModel ? LlmModelRole.Prompt : LlmModelRole.Story);
        }

        public async Task<string> GetCompletionAsync(IEnumerable<ChatMessage> messages, double temperature, int maxTokens, LlmModelRole role)
        {
            var messageList = messages.ToList();
            _logger.Log($"[{nameof(LlmClient)}] {nameof(GetCompletionAsync)} called. Temperature: {temperature}, MaxTokens: {maxTokens}. Message count: {messageList.Count}, Role: {role}");
            var config = GetModelConfig(role);

            if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.ApiUrl))
            {
                throw new InvalidOperationException($"API configuration is missing for {role} model.");
            }

            var request = CreateRequest(messageList, temperature, maxTokens, stream: false, config.ModelName, config.ApiUrl, config.ApiKey, role);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.Log($"LlmClient Error: {response.StatusCode} - {error}", LogLevel.Error);
                    throw new HttpRequestException($"API Error: {response.StatusCode} - {error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                
                return content ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"LlmClient.GetCompletionAsync.{role}");
                throw;
            }
        }

        public IAsyncEnumerable<string> StreamCompletionAsync(IEnumerable<ChatMessage> messages, double temperature, int maxTokens, bool isPromptModel = false)
        {
            return StreamCompletionAsync(messages, temperature, maxTokens, isPromptModel ? LlmModelRole.Prompt : LlmModelRole.Story);
        }

        public async IAsyncEnumerable<string> StreamCompletionAsync(
            IEnumerable<ChatMessage> messages,
            double temperature,
            int maxTokens,
            LlmModelRole role)
        {
            var messageList = messages.ToList();
            _logger.Log($"[{nameof(LlmClient)}] {nameof(StreamCompletionAsync)} called. Temperature: {temperature}, MaxTokens: {maxTokens}. Message count: {messageList.Count}, Role: {role}");
            var config = GetModelConfig(role);

            if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.ApiUrl))
            {
                yield return $"[Error: Configuration missing for {role} model]";
                yield break;
            }

            var request = CreateRequest(messageList, temperature, maxTokens, stream: true, config.ModelName, config.ApiUrl, config.ApiKey, role);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Log($"Stream API Error: {response.StatusCode} - {error}", LogLevel.Error);
                yield return $"[Error: {response.StatusCode} - {error}]";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                string? content = null;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() > 0)
                    {
                        var delta = choices[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentProp))
                        {
                            content = contentProp.GetString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Stream Parsing"); 
                }

                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }

        public int CalculateTokenCount(IEnumerable<ChatMessage> messages)
        {
            int tokenCount = 0;
            if (_tokenizer != null)
            {
                foreach (var msg in messages)
                {
                    tokenCount += _tokenizer.CountTokens(msg.Content ?? "") + 4;
                }
            }
            else
            {
                foreach (var msg in messages)
                {
                    tokenCount += (msg.Content?.Length ?? 0) / 4;
                }
            }
            return tokenCount;
        }

        private LlmModelConfig GetModelConfig(LlmModelRole role)
        {
            return role switch
            {
                LlmModelRole.Prompt => new LlmModelConfig(_settings.PromptModelName, _settings.PromptApiUrl, _settings.ApiKey),
                LlmModelRole.EntityParser => new LlmModelConfig(_settings.EntityParserModelName, _settings.EntityParserApiUrl, _settings.EntityParserApiKey),
                _ => new LlmModelConfig(_settings.StoryModelName, _settings.StoryApiUrl, _settings.ApiKey)
            };
        }

        private HttpRequestMessage CreateRequest(IEnumerable<ChatMessage> messages, double temperature, int maxTokens, bool stream, string modelName, string apiUrl, string apiKey, LlmModelRole role)
        {
            var endpoint = apiUrl;
            if (endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                endpoint += "/chat/completions";
            }

            var payload = new 
            {
                model = modelName,
                messages = messages,
                temperature = temperature,
                max_tokens = maxTokens,
                stream = stream
            };

            int tokenCount = CalculateTokenCount(messages);
            
            _logger.Log($"[{nameof(LlmClient)}] Calculated tokens: ~{tokenCount}. Role: {role}");
            if (role != LlmModelRole.EntityParser)
            {
                _toastService.ShowToast($"正在发送请求... (~{tokenCount} tokens)");
            }

            var lastMessage = messages.LastOrDefault();
            if (lastMessage != null)
            {
                _logger.Log($"[{nameof(LlmClient)}] Sending Prompt Content:{Environment.NewLine}{lastMessage.Content}");
            }
            
            var messagesJson = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            _logger.Log($"[{nameof(LlmClient)}] Full Conversation Content:{Environment.NewLine}{messagesJson}");
            
            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return request;
        }
    }

    public record LlmModelConfig(string ModelName, string ApiUrl, string ApiKey);
}
