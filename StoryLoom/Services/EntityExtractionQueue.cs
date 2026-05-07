using System.Collections.Concurrent;

namespace StoryLoom.Services;

public class EntityExtractionQueue
{
    private readonly ConcurrentQueue<EntityExtractionJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly EntityExtractionService _extractionService;
    private readonly EntityMergeService _mergeService;
    private readonly EntityChangeReviewService _reviewService;
    private readonly SettingsService _settings;
    private readonly LogService _logger;
    private readonly object _workerLock = new();
    private bool _isWorkerRunning;

    public event Func<Task>? OnEntitiesAutoApplied;

    public EntityExtractionQueue(EntityExtractionService extractionService, EntityMergeService mergeService, EntityChangeReviewService reviewService, SettingsService settings, LogService logger)
    {
        _extractionService = extractionService;
        _mergeService = mergeService;
        _reviewService = reviewService;
        _settings = settings;
        _logger = logger;
    }

    public void Enqueue(string content, string source, EntityChangeReviewMode mode = EntityChangeReviewMode.AiResponseReview)
    {
        if (!ShouldEnqueue(content, mode))
        {
            return;
        }

        _queue.Enqueue(new EntityExtractionJob(content, source, mode, DateTime.Now));
        _signal.Release();
        EnsureWorker();
    }

    private void EnsureWorker()
    {
        lock (_workerLock)
        {
            if (_isWorkerRunning)
            {
                return;
            }

            _isWorkerRunning = true;
            _ = Task.Run(ProcessQueueAsync);
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (true)
            {
                await _signal.WaitAsync();

                if (!_queue.TryDequeue(out var job))
                {
                    continue;
                }

                while (_reviewService.HasPendingChangeSet)
                {
                    await Task.Delay(250);
                }

                try
                {
                    _reviewService.NotifyExtractionStarted(job.Source);
                    _logger.Log($"Entity extraction started. Source: {job.Source}, Mode: {job.Mode}, Length: {job.Content.Length}");
                    var result = await _extractionService.ExtractAsync(job.Content);
                    var changeSet = _mergeService.CreateChangeSet(result, job.Source, job.Mode);
                    if (_settings.IsEntityParserAutoApplyEnabled)
                    {
                        var changed = _mergeService.Apply(changeSet);
                        _reviewService.NotifyAutoApplied(changeSet, changed);
                        if (changed && OnEntitiesAutoApplied != null)
                        {
                            await OnEntitiesAutoApplied.Invoke();
                        }
                        _logger.Log($"Entity extraction auto-applied. Mode: {job.Mode}, Change count: {changeSet.Count}, Changed: {changed}");
                    }
                    else
                    {
                        _reviewService.NotifyExtractionFinished(changeSet);
                        _logger.Log($"Entity extraction finished. Mode: {job.Mode}, Pending changes: {changeSet.Count}");
                    }
                }
                catch (EntityExtractionException ex)
                {
                    _reviewService.NotifyExtractionFailed($"实体解析失败：{ex.Message}");
                    _logger.LogError(ex, "EntityExtractionQueue.ProcessQueueAsync");
                }
                catch (Exception ex)
                {
                    _reviewService.NotifyExtractionFailed("实体解析失败，请查看日志");
                    _logger.LogError(ex, "EntityExtractionQueue.ProcessQueueAsync");
                }

                if (_queue.IsEmpty)
                {
                    lock (_workerLock)
                    {
                        if (_queue.IsEmpty)
                        {
                            _isWorkerRunning = false;
                            return;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EntityExtractionQueue.Worker");
            lock (_workerLock)
            {
                _isWorkerRunning = false;
            }
        }
    }

    private bool ShouldEnqueue(string content, EntityChangeReviewMode mode)
    {
        if (!_settings.IsEntityParserEnabled || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return mode switch
        {
            EntityChangeReviewMode.UserInputPreflight => _settings.IsEntityParserBeforeSendEnabled,
            EntityChangeReviewMode.AiResponseReview => _settings.IsEntityParserAfterSendEnabled,
            _ => true
        };
    }
}

public record EntityExtractionJob(string Content, string Source, EntityChangeReviewMode Mode, DateTime CreatedAt);
