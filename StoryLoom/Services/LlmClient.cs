using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.ML.Tokenizers;

namespace StoryLoom.Services
{
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

        public async Task<string> GetCompletionAsync(IEnumerable<ChatMessage> messages, double temperature, int maxTokens)
        {
            _logger.Log($"[{nameof(LlmClient)}] {nameof(GetCompletionAsync)} called. Temperature: {temperature}, MaxTokens: {maxTokens}. Message count: {messages.Count()}");
            
            if (!_settings.IsModelConfigured)
            {
                throw new InvalidOperationException("API configuration is missing.");
            }

            var request = CreateRequest(messages, temperature, maxTokens, stream: false);

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
                // Handle different response structures if needed, but standard OpenAI format is:
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                
                return content ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LlmClient.GetCompletionAsync");
                throw;
            }
        }

        public async IAsyncEnumerable<string> StreamCompletionAsync(IEnumerable<ChatMessage> messages, double temperature, int maxTokens)
        {
            _logger.Log($"[{nameof(LlmClient)}] {nameof(StreamCompletionAsync)} called. Temperature: {temperature}, MaxTokens: {maxTokens}. Message count: {messages.Count()}");

            if (!_settings.IsModelConfigured)
            {
                yield return "[Error: Configuration missing]";
                yield break;
            }

            var request = CreateRequest(messages, temperature, maxTokens, stream: true);

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
                    // Silent catch for chunk parsing errors to avoid breaking stream
                    _logger.LogError(ex, "Stream Parsing"); 
                }

                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }

        private HttpRequestMessage CreateRequest(IEnumerable<ChatMessage> messages, double temperature, int maxTokens, bool stream)
        {
            var endpoint = _settings.ApiUrl;
            if (endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                endpoint += "/chat/completions";
            }
            // Add other heuristic fix if necessary

            var payload = new 
            {
                model = _settings.ModelName,
                messages = messages,
                temperature = temperature,
                max_tokens = maxTokens,
                stream = stream
            };

            int tokenCount = 0;
            if (_tokenizer != null)
            {
                foreach (var msg in messages)
                {
                    tokenCount += _tokenizer.CountTokens(msg.Content ?? "") + 4; // basic heuristic
                }
            }
            
            _logger.Log($"[{nameof(LlmClient)}] Calculated tokens: ~{tokenCount}. Calling ToastService.");
            _toastService.ShowToast($"正在发送请求... (~{tokenCount} tokens)");

            var lastMessage = messages.LastOrDefault();
            if (lastMessage != null)
            {
                _logger.Log($"[{nameof(LlmClient)}] Sending Prompt Content:\n{lastMessage.Content}");
            }
            
            var messagesJson = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            _logger.Log($"[{nameof(LlmClient)}] Full Conversation Content:\n{messagesJson}");
            
            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return request;
        }
    }
}
