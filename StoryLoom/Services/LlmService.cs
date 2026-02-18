using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StoryLoom.Services
{
    public class LlmService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _httpClient;
        private readonly LogService _logger;

        public LlmService(SettingsService settings, HttpClient httpClient, LogService logger)
        {
            _settings = settings;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> TestConnectionAsync()
        {
            _logger.Log($"Testing connection to {_settings.ApiUrl} with model {_settings.ModelName}...");
            if (!_settings.IsModelConfigured)
            {
                _logger.Log("TestConnection failed: API configuration missing.", LogLevel.Warning);
                throw new InvalidOperationException("API configuration is missing.");
            }

            var request = CreateRequest(new[]
            {
                new ChatMessage { Role = "user", Content = "Hello, are you online? Reply with 'Yes' if you are." }
            }, stream: false);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                     var error = await response.Content.ReadAsStringAsync();
                     _logger.Log($"TestConnection Error: {response.StatusCode} - {error}", LogLevel.Error);
                     throw new HttpRequestException($"API Error: {response.StatusCode} - {error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                
                _logger.Log("TestConnection successful.");
                return content ?? "No response content.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TestConnectionAsync");
                throw;
            }
        }

        public async IAsyncEnumerable<string> StreamCompletionAsync(string userPrompt)
        {
            _logger.Log($"Starting stream completion for prompt: {userPrompt.Substring(0, Math.Min(50, userPrompt.Length))}...");
            if (!_settings.IsModelConfigured)
            {
                _logger.Log("StreamCompletion failed: Configuration missing.", LogLevel.Warning);
                yield break;
            }

            var messages = new List<ChatMessage>();
            
            // Add System Prompt (World Context) if available
            if (_settings.IsStoryContextReady)
            {
                var systemContent = $"You are a story co-author. \nWorld Background: {_settings.Background}\nProtagonist: {_settings.Protagonist}";
                messages.Add(new ChatMessage { Role = "system", Content = systemContent });
            }
            
            messages.Add(new ChatMessage { Role = "user", Content = userPrompt });

            var request = CreateRequest(messages, stream: true);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.Log($"StreamCompletion API Error: {response.StatusCode} - {error}", LogLevel.Error);
                yield return $"[Error: {response.StatusCode} - {error}]";
                yield break;
            }

            _logger.Log("Stream connection established. Reading chunks...");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new System.IO.StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]")
                {
                    _logger.Log("Stream completed [DONE].");
                    break;
                }

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
                     _logger.LogError(ex, "StreamCompletion parsing chunk");
                }

                if (!string.IsNullOrEmpty(content))
                {
                    yield return content;
                }
            }
        }

        public async Task<string> EnhanceTextAsync(string input, string type)
        {
             _logger.Log($"Enhancing text [{type}]...");
             if (!_settings.IsModelConfigured)
             {
                _logger.Log("EnhanceText failed: Configuration missing.", LogLevel.Warning);
                return input + " [AI Config Missing]";
             }

            var prompt = $"Please enhance and expand the following {type} description for a story. Keep it consistent but make it more vivid and detailed:\n\n{input}";
            
            var request = CreateRequest(new[]
            {
                new ChatMessage { Role = "user", Content = prompt }
            }, stream: false);

            try
            {
                using var response = await _httpClient.SendAsync(request);
                 if (!response.IsSuccessStatusCode)
                {
                     var error = await response.Content.ReadAsStringAsync();
                     _logger.Log($"EnhanceText Error: {response.StatusCode} - {error}", LogLevel.Error);
                     throw new HttpRequestException($"API Error: {response.StatusCode} - {error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var enhanced = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? input;
                _logger.Log($"Text enhanced successfully (Length: {enhanced.Length}).");
                return enhanced;
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "EnhanceTextAsync");
                 throw;
            }
        }

        private HttpRequestMessage CreateRequest(IEnumerable<ChatMessage> messages, bool stream)
        {
            var endpoint = _settings.ApiUrl;
            if (endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                endpoint += "/chat/completions";
            }
            else if (!endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase) && !endpoint.EndsWith("/"))
            {
                 // Optional: Be smart about other cases, or just leave it. 
                 // For now, fixing the specific /v1 case is safest.
                 // Let's add a generic check: if it looks like a base URL ensure it has chat/completions
            }
            
            var payload = new ChatRequest
            {
                Model = _settings.ModelName,
                Messages = messages,
                Temperature = _settings.Temperature,
                MaxTokens = _settings.MaxContextWindow,
                Stream = stream
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            
            return request;
        }
    }
    public class ChatMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }

    public class ChatRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("messages")]
        public IEnumerable<ChatMessage> Messages { get; set; } = Array.Empty<ChatMessage>();

        [System.Text.Json.Serialization.JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }
}
