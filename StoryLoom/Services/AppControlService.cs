using System.Windows;

namespace StoryLoom.Services;

public class AppControlService
{
    private readonly SettingsService _settingsService;
    private readonly LogService _logger;
    private readonly ConversationService _conversationService;
    private readonly LlmService _llmService;

    public AppControlService(SettingsService settingsService, LogService logger, ConversationService conversationService, LlmService llmService)
    {
        _settingsService = settingsService;
        _logger = logger;
        _conversationService = conversationService;
        _llmService = llmService;
    }

    public async Task PerformShutdownAsync()
    {
        _logger.Log("[AppControlService] Shutdown initiated by user.");
        
        try 
        {
            // Stop any ongoing generation (Requires CancellationToken support in LlmService in the future, 
            // but for now we ensure state is saved)
            
            // Save settings explicitly
            _settingsService.SaveConfig();
            
            // Save conversation explicitly
            await _conversationService.SaveCurrentStateAsync();
            
            _logger.Log("[AppControlService] State saved. Executing shutdown.");
        }
        catch (Exception ex)
        {
           _logger.LogError(ex, "PerformShutdownAsync");
        }
        finally 
        {
            Application.Current.Dispatcher.Invoke(() => {
                Application.Current.Shutdown();
            });
        }
    }
}
