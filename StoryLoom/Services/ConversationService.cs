using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace StoryLoom.Services
{
    public class ConversationService
    {
        private readonly LlmService _llmService;
        private readonly SettingsService _settingsService;
        private readonly LogService _logger;
        // Persistence Constants
        private const string SavesDirectory = "Saves";
        private const string WorldFile = "world.json";
        private const string ChatFile = "chat.json";

        public Conversation CurrentConversation { get; private set; } = new Conversation();
        public string CurrentSaveName { get; private set; } = "";

        public event Action? OnConversationUpdated;

        public ConversationService(LlmService llmService, SettingsService settingsService, LogService logger)
        {
            _llmService = llmService;
            _settingsService = settingsService;
            _logger = logger;
            // LoadHistory(); // Removed legacy single-file load
        }

        public async Task AddUserMessageAsync(string content)
        {
            _logger.Log($"[{nameof(ConversationService)}] {nameof(AddUserMessageAsync)} called. Message length: {content?.Length ?? 0}");
            CurrentConversation.Messages.Add(new ChatMessage { Role = "user", Content = content });
            NotifyUpdate();
            await SaveCurrentStateAsync();
            await CheckAndSummarizeAsync();
        }

        public async Task AddAiMessageAsync(string content)
        {
            _logger.Log($"[{nameof(ConversationService)}] {nameof(AddAiMessageAsync)} called. Message length: {content?.Length ?? 0}");
            CurrentConversation.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
            NotifyUpdate();
            await SaveCurrentStateAsync();
            await CheckAndSummarizeAsync();
        }

        public async Task StartNewConversationAsync()
        {
            _logger.Log("Starting new conversation/save...");

            // 1. Generate unique save name (e.g., Save_yyyyMMdd_HHmmss)
            string saveName = $"Save_{DateTime.Now:yyyyMMdd_HHmmss}";
            CurrentSaveName = saveName;
            
            // 2. Clear state
            CurrentConversation = new Conversation
            {
                Id = Guid.NewGuid().ToString(),
                Title = "New Story",
                CreatedAt = DateTime.Now
            };
            
            // Reset World Settings in SettingsService (User needs to input new ones)
            _settingsService.Background = "";
            _settingsService.Protagonist = "";
            _settingsService.NotifyStateChanged();

            // 3. Create Directory
            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory, saveName);
            Directory.CreateDirectory(savePath);

            // 4. Update Global Config
            _settingsService.LastSaveName = saveName;
            _settingsService.SaveConfig();

            NotifyUpdate();
            await SaveCurrentStateAsync();
        }

        public async Task LoadLatestSaveAsync()
        {
             string lastSave = _settingsService.LastSaveName;
             if (!string.IsNullOrWhiteSpace(lastSave))
             {
                 string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory, lastSave);
                 if (Directory.Exists(savePath))
                 {
                     await LoadSaveAsync(lastSave);
                 }
                 else
                 {
                     _logger.Log($"Last save '{lastSave}' not found on disk. Starting new conversation.");
                     await StartNewConversationAsync();
                 }
             }
             else
             {
                 _logger.Log("No last save found in config. Starting new conversation.");
                 await StartNewConversationAsync();
             }
        }

        public async Task LoadSaveAsync(string saveName)
        {
            try
            {
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory, saveName);
                if (!Directory.Exists(savePath))
                {
                    _logger.Log($"Save directory not found: {savePath}", LogLevel.Warning);
                    return;
                }

                CurrentSaveName = saveName;
                _logger.Log($"Loading save: {saveName}");

                // 1. Load Chat
                string chatPath = Path.Combine(savePath, ChatFile);
                if (File.Exists(chatPath))
                {
                    string chatJson = await File.ReadAllTextAsync(chatPath);
                    CurrentConversation = JsonSerializer.Deserialize<Conversation>(chatJson) ?? new Conversation();
                }
                else
                {
                    CurrentConversation = new Conversation();
                }

                // 2. Load World
                string worldPath = Path.Combine(savePath, WorldFile);
                if (File.Exists(worldPath))
                {
                    string worldJson = await File.ReadAllTextAsync(worldPath);
                    var worldData = JsonSerializer.Deserialize<WorldSettings>(worldJson);
                    if (worldData != null)
                    {
                        _settingsService.Background = worldData.Background;
                        _settingsService.Protagonist = worldData.Protagonist;
                        _settingsService.NotifyStateChanged();
                    }
                }

                NotifyUpdate();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load save: {saveName}");
            }
        }

        public void UpdateTitle(string newTitle)
        {
            _logger.Log($"[{nameof(ConversationService)}] {nameof(UpdateTitle)} called. New title: {newTitle}");
            CurrentConversation.Title = newTitle;
            NotifyUpdate();
            _ = SaveCurrentStateAsync();
        }

        public List<ChatMessage> GetHistoryForLlm()
        {
            _logger.Log($"[{nameof(ConversationService)}] {nameof(GetHistoryForLlm)} called.");
            // Get recent history messages starting from the last summarized index
            // This excludes the system prompt, which is now handled by LlmService
            return CurrentConversation.Messages.Skip(CurrentConversation.LastSummarizedIndex).ToList();
        }

        public async Task SaveCurrentStateAsync()
        {
            _logger.Log($"[{nameof(ConversationService)}] {nameof(SaveCurrentStateAsync)} called.");
            if (string.IsNullOrWhiteSpace(CurrentSaveName))
            {
                _logger.Log("No save name specified. Cannot save state.", LogLevel.Error);
                return;
            }

            try
            {
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory, CurrentSaveName);
                if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);

                // 1. Save Chat
                string chatPath = Path.Combine(savePath, ChatFile);
                string chatJson = JsonSerializer.Serialize(CurrentConversation, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(chatPath, chatJson);

                // 2. Save World
                var worldData = new WorldSettings
                {
                    Background = _settingsService.Background,
                    Protagonist = _settingsService.Protagonist
                };
                string worldPath = Path.Combine(savePath, WorldFile);
                string worldJson = JsonSerializer.Serialize(worldData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(worldPath, worldJson);
                
                _logger.Log($"Saved state to {CurrentSaveName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save current state");
            }
        }

        private async Task CheckAndSummarizeAsync()
        {
            int maxTurns = _settingsService.MaxHistoryTurns;
            int totalMessages = CurrentConversation.Messages.Count;
            int unsummarizedCount = totalMessages - CurrentConversation.LastSummarizedIndex;

            // Trigger if we have more than maxTurns * 2 new messages since last summary
            if (unsummarizedCount > maxTurns * 2)
            {
                _logger.Log($"Conversation new messages ({unsummarizedCount}) exceeded limit. Summarizing...");
                
                // We want to keep the last 'keepCount' messages raw for context
                // Update: User requested to discard previous context after summary, so we keep 0 (summarize everything).
                int keepCount = 0;
                
                // The range to summarize is from LastSummarizedIndex up to (Total - KeepCount)
                int endIndex = totalMessages - keepCount;
                
                if (endIndex <= CurrentConversation.LastSummarizedIndex) return;

                var messagesToSummarize = CurrentConversation.Messages
                    .Skip(CurrentConversation.LastSummarizedIndex)
                    .Take(endIndex - CurrentConversation.LastSummarizedIndex)
                    .ToList();

                string textToSummarize = string.Join("\n", messagesToSummarize.Select(m => $"{m.Role}: {m.Content}"));
                
                // Update summary
                string newSummary = await _llmService.SummarizeTextAsync(textToSummarize, CurrentConversation.Summary);

                CurrentConversation.Summary = newSummary;
                CurrentConversation.LastSummarizedIndex = endIndex;
                
                _logger.Log($"Summarized {messagesToSummarize.Count} messages. New LastSummarizedIndex: {CurrentConversation.LastSummarizedIndex}");

                NotifyUpdate();
                await SaveCurrentStateAsync();
            }
        }

        private async Task<string> SummarizeConversationAsync(Conversation conversation)
        {
             if (!conversation.Messages.Any()) return conversation.Summary;
             // Summarize ONLY the unsummarized part to update the final summary? 
             // Or summarize everything?
             // If we have a running summary, we just need to summarize the remaining tail.
             
             var tailMessages = conversation.Messages.Skip(conversation.LastSummarizedIndex).ToList();
             if (!tailMessages.Any()) return conversation.Summary;

             string text = string.Join("\n", tailMessages.Select(m => $"{m.Role}: {m.Content}"));
             return await _llmService.SummarizeTextAsync(text, conversation.Summary);
        }

        // Removed ArchiveConversationAsync as we now use persistent folders

        // Removed LoadHistory/SaveHistoryAsync legacy methods

        private void NotifyUpdate() => OnConversationUpdated?.Invoke();

        private class WorldSettings
        {
            public string Background { get; set; } = "";
            public string Protagonist { get; set; } = "";
        }
    }

    public class Conversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "New Story";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Summary { get; set; } = "";
        
        /// <summary>
        /// Index of the first message that hasn't been "fully" summarized into the Summary string yet.
        /// Messages before this index are considered "archived" into the Summary for LLM context purposes,
        /// but are still kept here for UI display history.
        /// </summary>
        public int LastSummarizedIndex { get; set; } = 0;
        
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
