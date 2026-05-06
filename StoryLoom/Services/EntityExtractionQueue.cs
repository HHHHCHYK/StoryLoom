using System.Collections.Concurrent;

namespace StoryLoom.Services;

public class EntityExtractionQueue
{
    private readonly ConcurrentQueue<EntityExtractionJob> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly EntityExtractionService _extractionService;
    private readonly EntityMergeService _mergeService;
    private readonly SettingsService _settings;
    private readonly LogService _logger;
    private readonly object _workerLock = new();
    private bool _isWorkerRunning;

    public EntityExtractionQueue(EntityExtractionService extractionService, EntityMergeService mergeService, SettingsService settings, LogService logger)
    {
        _extractionService = extractionService;
        _mergeService = mergeService;
        _settings = settings;
        _logger = logger;
    }

    public void Enqueue(string content, string source)
    {
        if (!_settings.IsEntityParserEnabled || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        _queue.Enqueue(new EntityExtractionJob(content, source, DateTime.Now));
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

                try
                {
                    _logger.Log($"Entity extraction started. Source: {job.Source}, Length: {job.Content.Length}");
                    var result = await _extractionService.ExtractAsync(job.Content);
                    var changed = _mergeService.Merge(result);
                    _logger.Log($"Entity extraction finished. Changed: {changed}");
                }
                catch (Exception ex)
                {
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
}

public record EntityExtractionJob(string Content, string Source, DateTime CreatedAt);
