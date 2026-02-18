using System;

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

        private readonly LogService _logger;

        /// <summary>
        /// 初始化配置服务。
        /// </summary>
        public SettingsService(LogService logger)
        {
            _logger = logger;
            _logger.Log("SettingsService initialized.");
        }

        // Events to notify components of changes
        public event Action? OnChange;

        public void NotifyStateChanged()
        {
            _logger.Log("Settings state changed.", LogLevel.Info);
            OnChange?.Invoke();
        }
    }
}
