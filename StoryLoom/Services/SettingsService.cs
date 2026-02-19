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
        /// <summary>模型名称 (例如: deepseek-chat)。</summary>
        public string ModelName { get; set; } = "deepseek-chat"; 
        /// <summary>模型 API 的基础地址。</summary>
        public string ApiUrl { get; set; } = "https://api.deepseek.com/v1";
        /// <summary>API 访问密钥 (Key)。</summary>
        public string ApiKey { get; set; } = "";
        /// <summary>温度值 (Temperature)，控制生成的随机性 (0.0 - 1.0)。</summary>
        public double Temperature { get; set; } = 0.7;
        /// <summary>最大上下文窗口大小 (Tokens)。</summary>
        public int MaxContextWindow { get; set; } = 4096;

        /// <summary>最大历史对话轮数 (Turns)。超过此数量将触发总结。</summary>
        public int MaxHistoryTurns { get; set; } = 10;

        // Story Context
        /// <summary>故事背景设定。</summary>
        public string Background { get; set; } = "";
        /// <summary>主角设定。</summary>
        public string Protagonist { get; set; } = "";

        // Helper to check if model is configured
        /// <summary>判断模型是否已配置（检查 Key 和 URL 是否非空）。</summary>
        public bool IsModelConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiUrl);

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
                        ModelName = config.ModelName;
                        ApiUrl = config.ApiUrl;
                        ApiKey = config.ApiKey;
                        Temperature = config.Temperature;
                        MaxContextWindow = config.MaxContextWindow;
                        MaxHistoryTurns = config.MaxHistoryTurns;
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
                    ModelName = ModelName,
                    ApiUrl = ApiUrl,
                    ApiKey = ApiKey,
                    Temperature = Temperature,
                    MaxContextWindow = MaxContextWindow,
                    MaxHistoryTurns = MaxHistoryTurns,
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
            public string ModelName { get; set; } = "deepseek-chat";
            public string ApiUrl { get; set; } = "https://api.deepseek.com/v1";
            public string ApiKey { get; set; } = "";
            public double Temperature { get; set; } = 0.7;
            public int MaxContextWindow { get; set; } = 4096;
            public int MaxHistoryTurns { get; set; } = 10;
            public string LastSaveName { get; set; } = "";
        }
    }
}
