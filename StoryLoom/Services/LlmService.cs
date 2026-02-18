using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StoryLoom.Services
{
    /// <summary>
    /// 大语言模型 (LLM) 服务类。
    /// 负责通过 HTTP API 与大模型进行交互，包括测试连接、流式生成文本和文本润色等功能。
    /// </summary>
    public class LlmService
    {
        private readonly SettingsService _settings;
        private readonly HttpClient _httpClient;
        private readonly LogService _logger;

        /// <summary>
        /// 初始化 <see cref="LlmService"/> 类的新实例。
        /// </summary>
        /// <param name="settings">配置服务，用于获取 API URL、Key 等设置。</param>
        /// <param name="httpClient">HTTP 客户端，用于发送网络请求。</param>
        /// <param name="logger">日志服务，用于系统日志记录。</param>
        public LlmService(SettingsService settings, HttpClient httpClient, LogService logger)
        {
            _settings = settings;
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// 测试与 LLM API 的连接状态。
        /// 发送一个简单的握手请求以验证配置是否正确。
        /// </summary>
        /// <returns>若连接成功，返回 API 的响应内容。</returns>
        /// <exception cref="InvalidOperationException">当 API 未配置时抛出。</exception>
        /// <exception cref="HttpRequestException">当网络请求失败或 API 返回错误时抛出。</exception>
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

        /// <summary>
        /// 流式生成文本。
        /// 通过 Server-Sent Events (SSE) 接收大模型的流式响应，实现打字机效果。
        /// </summary>
        /// <param name="userPrompt">用户的输入提示词。</param>
        /// <returns>异步字符串流，包含按顺序生成的文本片段。</returns>
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

        /// <summary>
        /// 文本润色/扩写功能。
        /// 将简短的描述扩展为更丰富、生动的内容。此方法使用非流式请求。
        /// </summary>
        /// <param name="input">需要润色的原始文本。</param>
        /// <param name="type">文本类型（如 "Background" 背景, "Protagonist" 主角），用于构建特定的 Prompt。</param>
        /// <returns>润色后的完整文本。</returns>
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

        /// <summary>
        /// 创建 HTTP 请求的辅助方法。
        /// 负责构建请求 URL（自动处理 /chat/completions 路径）和序列化请求体。
        /// </summary>
        /// <param name="messages">聊天消息历史列表。</param>
        /// <param name="stream">是否开启流式模式。</param>
        /// <returns>配置好的 HttpRequestMessage 对象。</returns>
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
