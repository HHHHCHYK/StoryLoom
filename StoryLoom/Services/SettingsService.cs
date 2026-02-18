using System;

namespace StoryLoom.Services
{
    public class SettingsService
    {
        // Model Settings
        public string ModelName { get; set; } = "deepseek-chat"; 
        public string ApiUrl { get; set; } = "https://api.deepseek.com/v1";
        public string ApiKey { get; set; } = "";
        public double Temperature { get; set; } = 0.7;
        public int MaxContextWindow { get; set; } = 4096;

        // Story Context
        public string Background { get; set; } = "";
        public string Protagonist { get; set; } = "";

        // Helper to check if model is configured
        public bool IsModelConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiUrl);

        // Helper to check if story context is ready
        public bool IsStoryContextReady => !string.IsNullOrWhiteSpace(Background) && !string.IsNullOrWhiteSpace(Protagonist);

        private readonly LogService _logger;

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
