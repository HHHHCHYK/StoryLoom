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
        private const string ConversationHistoryFile = "conversation_history.json";
        private const string ArchivedConversationsDir = "archived_conversations";

        public Conversation CurrentConversation { get; private set; } = new Conversation();
        public event Action? OnConversationUpdated;

        public ConversationService(LlmService llmService, SettingsService settingsService, LogService logger)
        {
            _llmService = llmService;
            _settingsService = settingsService;
            _logger = logger;
            LoadHistory();
        }

        public async Task AddUserMessageAsync(string content)
        {
            CurrentConversation.Messages.Add(new ChatMessage { Role = "user", Content = content });
            NotifyUpdate();
            await SaveHistoryAsync();
            await CheckAndSummarizeAsync();
        }

        public async Task AddAiMessageAsync(string content)
        {
            CurrentConversation.Messages.Add(new ChatMessage { Role = "assistant", Content = content });
            NotifyUpdate();
            await SaveHistoryAsync();
            await CheckAndSummarizeAsync();
        }

        public async Task StartNewConversationAsync()
        {
            _logger.Log("Starting new conversation...");
            
            // Archive current conversation
            if (CurrentConversation.Messages.Any())
            {
                // Summarize current conversation for context
                string summary = await SummarizeConversationAsync(CurrentConversation);
                
                // Ensure the final summary is saved to the conversation object before archiving
                CurrentConversation.Summary = summary;
                
                await ArchiveConversationAsync(CurrentConversation);

                // Start new conversation with summary
                CurrentConversation = new Conversation
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = "New Story", // Default title
                    CreatedAt = DateTime.Now,
                    Summary = summary, // Pass previous context
                    LastSummarizedIndex = 0 // Reset summary index for new conversation
                };
            }
            else
            {
                // Just reset if empty
                 CurrentConversation = new Conversation
                {
                     Id = Guid.NewGuid().ToString(),
                     CreatedAt = DateTime.Now
                };
            }
            
            NotifyUpdate();
            await SaveHistoryAsync();
        }

        public void UpdateTitle(string newTitle)
        {
            CurrentConversation.Title = newTitle;
            NotifyUpdate();
            _ = SaveHistoryAsync();
        }

        public List<ChatMessage> GetContextForLlm()
        {
            // Implementation of context retrieval: System Prompt + Summary + Recent Unsummarized Messages
            var context = new List<ChatMessage>();

            // 1. System Prompt construction
            var systemContent = $"You are a story co-author. \nWorld Background: {_settingsService.Background}\nProtagonist: {_settingsService.Protagonist}";
            
            if (!string.IsNullOrWhiteSpace(CurrentConversation.Summary))
            {
                systemContent += $"\n\nPrevious Story Summary: {CurrentConversation.Summary}";
            }

            context.Add(new ChatMessage { Role = "system", Content = systemContent });

            // 2. Recent History (Messages that haven't been forcefully summarized out implies we keep all messages in UI, 
            // but for LLM we might want to drop very old ones if they are covered by summary? 
            // The plan said: "Summarize the oldest messages... maintain context within token limits".
            // Since we update Summary incrementally, we should theoretically only send the Summary + Messages AFTER LastSummarizedIndex?
            // However, to ensure smooth continuity, we might want some overlap or just trust the summary + active window.
            // Let's send messages starting from LastSummarizedIndex.
            
            var recentMessages = CurrentConversation.Messages.Skip(CurrentConversation.LastSummarizedIndex).ToList();
            context.AddRange(recentMessages);

            return context;
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
                int keepCount = 4; // Keep last 2 turns
                
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
                await SaveHistoryAsync();
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

        private async Task ArchiveConversationAsync(Conversation conversation)
        {
             if (!Directory.Exists(ArchivedConversationsDir))
             {
                 Directory.CreateDirectory(ArchivedConversationsDir);
             }
             string filename = Path.Combine(ArchivedConversationsDir, $"conversation_{conversation.Id}_{DateTime.Now:yyyyMMddHHmmss}.json");
             string json = JsonSerializer.Serialize(conversation);
             await File.WriteAllTextAsync(filename, json);
             _logger.Log($"Archived conversation to {filename}");
        }

        private void LoadHistory()
        {
            if (File.Exists(ConversationHistoryFile))
            {
                try
                {
                    string json = File.ReadAllText(ConversationHistoryFile);
                    CurrentConversation = JsonSerializer.Deserialize<Conversation>(json) ?? new Conversation();
                    _logger.Log("Loaded conversation history.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load conversation history");
                    CurrentConversation = new Conversation();
                }
            }
        }

        private async Task SaveHistoryAsync()
        {
            try
            {
                string json = JsonSerializer.Serialize(CurrentConversation);
                await File.WriteAllTextAsync(ConversationHistoryFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save conversation history");
            }
        }

        private void NotifyUpdate() => OnConversationUpdated?.Invoke();
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
