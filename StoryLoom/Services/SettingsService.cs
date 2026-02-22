using System;
using System.IO;
using System.Text.Json;

namespace StoryLoom.Services
{
    /// <summary>
    /// 设置服务类。
    /// 管理应用程序的全局配置（如模型设置）和运行时状态（如故事背景、主角设定）。
    /// </summary>
    public class SettingsService
    {
        // Model Settings
        // Model Settings
        /// <summary>正文模型名称 (例如: deepseek-chat)。用于生成故事内容。</summary>
        public string StoryModelName { get; set; } = "deepseek-chat"; 
        /// <summary>正文模型 API 的基础地址。</summary>
        public string StoryApiUrl { get; set; } = "https://api.deepseek.com/v1";

        /// <summary>提示模型名称 (例如: deepseek-chat)。用于总结、建议等后台任务。</summary>
        public string PromptModelName { get; set; } = "deepseek-chat";
        /// <summary>提示模型 API 的基础地址。</summary>
        public string PromptApiUrl { get; set; } = "https://api.deepseek.com/v1";

        /// <summary>API 访问密钥 (Key)。所有模型共享此 Key。</summary>
        public string ApiKey { get; set; } = "";
        /// <summary>温度值 (Temperature)，控制生成的随机性 (0.0 - 1.0)。</summary>
        public double Temperature { get; set; } = 0.7;
        /// <summary>最大上下文窗口大小 (Tokens)。</summary>
        public int MaxContextWindow { get; set; } = 4096;

        /// <summary>触发总结的 Tokens 数量阈值。总发送内容超过此数值将先总结再发送。</summary>
        public int SummaryTokenThreshold { get; set; } = 4000;

        /// <summary>打字机输出速度 (毫秒/字符)。</summary>
        public int TextSpeed { get; set; } = 30;

        // Story Context
        /// <summary>故事背景设定。</summary>
        public string Background { get; set; } = "";
        /// <summary>主角设定。</summary>
        public string Protagonist { get; set; } = "";

        // Helper to check if model is configured
        /// <summary>判断正文模型是否已配置（检查 Key 和 URL 是否非空）。</summary>
        public bool IsStoryModelConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(StoryApiUrl);

        /// <summary>判断提示模型是否已配置（检查 Key 和 URL 是否非空）。</summary>
        public bool IsPromptModelConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(PromptApiUrl);

        /// <summary>判断是否所有模型都已配置。</summary>
        public bool IsModelConfigured => IsStoryModelConfigured && IsPromptModelConfigured;

        // Helper to check if story context is ready
        /// <summary>判断故事上下文是否就绪（检查背景和主角是否已设定）。</summary>
        public bool IsStoryContextReady => !string.IsNullOrWhiteSpace(Background) && !string.IsNullOrWhiteSpace(Protagonist);

        // Persistence
        public string LastSaveName { get; set; } = "";

        private const string ConfigDirectory = "Config";
        private const string ConfigFile = "config.json";

        private readonly LogService _logger;

        /// <summary>
        /// 初始化配置服务。
        /// </summary>
        public SettingsService(LogService logger)
        {
            _logger = logger;
            _logger.Log("SettingsService initialized.");
            LoadConfig();
        }

        public void LoadConfig()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigDirectory);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string configPath = Path.Combine(configDir, ConfigFile);
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<SettingsConfig>(json);
                    if (config != null)
                    {
                        StoryModelName = !string.IsNullOrEmpty(config.StoryModelName) ? config.StoryModelName : config.ModelName ?? "deepseek-chat";
                        StoryApiUrl = !string.IsNullOrEmpty(config.StoryApiUrl) ? config.StoryApiUrl : config.ApiUrl ?? "https://api.deepseek.com/v1";
                        PromptModelName = !string.IsNullOrEmpty(config.PromptModelName) ? config.PromptModelName : StoryModelName;
                        PromptApiUrl = !string.IsNullOrEmpty(config.PromptApiUrl) ? config.PromptApiUrl : StoryApiUrl;

                        ApiKey = config.ApiKey;
                        Temperature = config.Temperature;
                        MaxContextWindow = config.MaxContextWindow;
                        SummaryTokenThreshold = config.SummaryTokenThreshold;
                        TextSpeed = config.TextSpeed;
                        LastSaveName = config.LastSaveName;
                        _logger.Log("Global configuration loaded.");
                    }
                }
                else
                {
                    // Create default config
                    _logger.Log("Config file not found. Creating default.");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load global config");
            }
        }

        public void SaveConfig()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigDirectory);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var config = new SettingsConfig
                {
                    StoryModelName = StoryModelName,
                    StoryApiUrl = StoryApiUrl,
                    PromptModelName = PromptModelName,
                    PromptApiUrl = PromptApiUrl,
                    ApiKey = ApiKey,
                    Temperature = Temperature,
                    MaxContextWindow = MaxContextWindow,
                    SummaryTokenThreshold = SummaryTokenThreshold,
                    TextSpeed = TextSpeed,
                    LastSaveName = LastSaveName
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(configDir, ConfigFile), json);
                _logger.Log("Global configuration saved.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save global config");
            }
        }

        // Events to notify components of changes
        public event Action? OnChange;

        public void NotifyStateChanged()
        {
            _logger.Log("Settings state changed.", LogLevel.Info);
            SaveConfig(); // Auto-save on change? Or explicit save? Let's auto-save for now.
            OnChange?.Invoke();
        }

        // Inner class for serialization to strictly control what gets saved to config.json
        private class SettingsConfig
        {
            // For backward compatibility when deserializing old config.json
            public string? ModelName { get; set; }
            public string? ApiUrl { get; set; }

            public string StoryModelName { get; set; } = "deepseek-chat";
            public string StoryApiUrl { get; set; } = "https://api.deepseek.com/v1";
            public string PromptModelName { get; set; } = "deepseek-chat";
            public string PromptApiUrl { get; set; } = "https://api.deepseek.com/v1";
            public string ApiKey { get; set; } = "";
            public double Temperature { get; set; } = 0.7;
            public int MaxContextWindow { get; set; } = 4096;
            public int SummaryTokenThreshold { get; set; } = 4000;
            public int TextSpeed { get; set; } = 30;
            public string LastSaveName { get; set; } = "";
        }
    }
}
