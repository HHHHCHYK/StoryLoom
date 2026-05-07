namespace StoryLoom.Services;

public class EntityChangeReviewService
{
    private readonly ToastService _toastService;
    private readonly LogService _logger;

    public EntityChangeSet? PendingChangeSet { get; private set; }
    public bool HasPendingChangeSet => PendingChangeSet != null;
    public bool IsExtracting { get; private set; }
    public string? LastError { get; private set; }

    public event Action? OnChange;

    public EntityChangeReviewService(ToastService toastService, LogService logger)
    {
        _toastService = toastService;
        _logger = logger;
    }

    public void NotifyExtractionStarted(string source)
    {
        IsExtracting = true;
        LastError = null;
        _toastService.ShowToast("正在解析实体...");
        _logger.Log($"Entity extraction review started. Source: {source}");
        NotifyStateChanged();
    }

    public void NotifyExtractionFinished(EntityChangeSet changeSet)
    {
        IsExtracting = false;
        PendingChangeSet = changeSet.Count > 0 ? changeSet : null;

        if (PendingChangeSet == null)
        {
            _toastService.ShowToast("实体解析完成，未发现可写入的变化");
        }
        else
        {
            _toastService.ShowToast($"实体解析完成，发现 {PendingChangeSet.Count} 项变化，等待确认写入");
        }

        _logger.Log($"Entity extraction review finished. Change count: {changeSet.Count}");
        NotifyStateChanged();
    }

    public void NotifyAutoApplied(EntityChangeSet changeSet, bool changed)
    {
        IsExtracting = false;
        PendingChangeSet = null;
        LastError = null;

        if (changeSet.Count == 0)
        {
            _toastService.ShowToast("实体解析完成，未发现可写入的变化");
        }
        else if (changed)
        {
            _toastService.ShowToast($"实体解析完成，已自动写入 {changeSet.Count} 项变化");
        }
        else
        {
            _toastService.ShowToast("实体解析完成，未产生新的世界数据变化");
        }

        _logger.Log($"Entity extraction review auto-applied. Change count: {changeSet.Count}, Changed: {changed}");
        NotifyStateChanged();
    }

    public void NotifyExtractionFailed(string message)
    {
        IsExtracting = false;
        PendingChangeSet = null;
        LastError = message;
        _toastService.ShowToast(message);
        NotifyStateChanged();
    }

    public EntityChangeSet? TakePendingChangeSet()
    {
        var changeSet = PendingChangeSet;
        PendingChangeSet = null;
        NotifyStateChanged();
        return changeSet;
    }

    public void DismissPendingChangeSet()
    {
        PendingChangeSet = null;
        _toastService.ShowToast("已取消本次实体写入");
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnChange?.Invoke();
    }
}
