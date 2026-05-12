# Writing Skill 系统实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 StoryLoom 中实现作品内 Writing Skill 系统，让每个存档可以管理多个写作 Skill、单选启用、保存版本、AI 自进化并接入故事生成。

**Architecture:** 新增 `WritingSkillService` 作为唯一读写 `writing-skills.json` 的入口；`ConversationService` 只负责在新建和加载存档时切换 Skill 库；`StoryGenerator.razor` 读取当前启用 Skill 并传入 `LlmService`，最终由 `PromptTemplates` 注入系统提示词。

**Tech Stack:** .NET 10、C#、WPF、Blazor WebView、Razor Components、System.Text.Json、OpenAI-style Chat Completions API。

---

## Review 吸收结论

本计划吸收 `docs/superpowers/specs/2026-05-12-writing-skill-system-design-review.md` 中的实现建议：

- `WritingSkillProfile.Name` 作为当前版本 `Snapshot.Name` 的镜像字段保留，所有创建新版本的服务方法必须同步它。
- `WritingSkillProfile.UpdatedAt` 只在创建新版本时更新，切换当前启用 Skill 不更新。
- 注入提示词前必须对 Skill 字段做 XML 转义。
- `LoadLibraryAsync` 和 `StartNewLibraryAsync` 必须清空 AI 进化候选状态。
- `WritingSkillService` 注册为 Singleton。
- `WritingSkills.razor` 和 `StoryGenerator.razor` 都订阅并释放 `WritingSkillService.OnChanged`。
- `StartGenerateAsync` 新增 nullable 默认参数 `WritingSkillSnapshot? activeWritingSkill = null`。
- `<WritingSkill>` 放在 `<Protagonist>` 之后、`<CurrentStoryState_Summary>` 之前。
- AI 自进化使用提示词模型，`maxTokens` 使用 4096；v1 只做 JSON 格式校验，内容质量由用户预览确认。
- 写入失败遵循 `ConversationService.SaveCurrentStateAsync` 模式：记录日志，不向 UI 抛出异常。
- Story 页 Skill 选择器采用紧凑控件，导航新增项放在末尾编号 `05`。

## 文件结构

- Create: `StoryLoom/Data/Models/WritingSkill.cs`
  - 定义 Writing Skill 库、Profile、Version、Snapshot 和版本来源枚举。
- Create: `StoryLoom/Services/WritingSkillService.cs`
  - 管理当前存档 Skill 库、持久化、版本、回滚、AI 进化候选和事件。
- Modify: `StoryLoom/Display/MainWindow.xaml.cs`
  - 注册 `WritingSkillService` 为 Singleton。
- Modify: `StoryLoom/Services/ConversationService.cs`
  - 构造函数注入 `WritingSkillService`。
  - 新建存档时初始化 Skill 库。
  - 加载存档时加载 Skill 库。
- Modify: `StoryLoom/Services/PromptTemplates.cs`
  - 扩展故事生成提示词，注入 XML 转义后的 `<WritingSkill>`。
  - 新增 AI 自进化提示词模板。
- Modify: `StoryLoom/Services/LlmService.cs`
  - 扩展 `StartGenerateAsync` 签名。
  - 新增 `EvolveWritingSkillAsync`，调用提示词模型并解析 JSON。
- Modify: `StoryLoom/Display/Pages/StoryGenerator.razor`
  - 注入 `WritingSkillService`。
  - 工具栏增加紧凑 Skill 选择器。
  - 生成时传入当前启用 Skill。
  - 订阅和释放 `OnChanged`。
- Create: `StoryLoom/Display/Pages/WritingSkills.razor`
  - 实现 Skill 管理页面、版本历史、回滚、AI 进化候选预览。
- Modify: `StoryLoom/Display/Shared/NavMenu.razor`
  - 增加 `05 写作 Skill` 导航入口。

---

### Task 1: 添加 Writing Skill 数据模型

**Files:**
- Create: `StoryLoom/Data/Models/WritingSkill.cs`

- [ ] **Step 1: 创建模型文件**

使用以下完整内容创建 `StoryLoom/Data/Models/WritingSkill.cs`：

```csharp
namespace StoryLoom.Data.Models;

[Serializable]
public class WritingSkillLibrary
{
    public int SaveVersion { get; set; } = 1;
    public string ActiveSkillId { get; set; } = string.Empty;
    public List<WritingSkillProfile> Skills { get; set; } = [];
}

[Serializable]
public class WritingSkillProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CurrentVersionId { get; set; } = string.Empty;
    public List<WritingSkillVersion> Versions { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public WritingSkillVersion? CurrentVersion =>
        Versions.FirstOrDefault(version => version.Id == CurrentVersionId);

    public WritingSkillSnapshot? CurrentSnapshot => CurrentVersion?.Snapshot;
}

[Serializable]
public class WritingSkillVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int VersionNumber { get; set; }
    public WritingSkillVersionSource Source { get; set; }
    public string ParentVersionId { get; set; } = string.Empty;
    public WritingSkillSnapshot Snapshot { get; set; } = new();
    public string UserNote { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

[Serializable]
public class WritingSkillSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string StyleDescription { get; set; } = string.Empty;
    public string WritingRules { get; set; } = string.Empty;
    public string WritingProcess { get; set; } = string.Empty;
    public string ForbiddenPatterns { get; set; } = string.Empty;
    public string ExampleText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public WritingSkillSnapshot Clone() => new()
    {
        Name = Name,
        Purpose = Purpose,
        StyleDescription = StyleDescription,
        WritingRules = WritingRules,
        WritingProcess = WritingProcess,
        ForbiddenPatterns = ForbiddenPatterns,
        ExampleText = ExampleText,
        Notes = Notes
    };
}

public enum WritingSkillVersionSource
{
    Initial,
    ManualEdit,
    AiEvolution,
    Rollback
}
```

- [ ] **Step 2: 构建验证模型可编译**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: build 成功，或只暴露与现有工作区无关的既有错误。若出现 `WritingSkill` 类型相关编译错误，修正模型文件。

---

### Task 2: 实现 WritingSkillService 持久化和版本操作

**Files:**
- Create: `StoryLoom/Services/WritingSkillService.cs`

- [ ] **Step 1: 创建服务骨架和属性**

创建 `StoryLoom/Services/WritingSkillService.cs`，先写入以下服务结构：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using StoryLoom.Data.Models;

namespace StoryLoom.Services;

public class WritingSkillService
{
    private const string SavesDirectory = "Saves";
    private const string WritingSkillsFile = "writing-skills.json";
    private readonly LogService _logger;
    private readonly LlmService _llmService;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public WritingSkillLibrary Library { get; private set; } = new();
    public string CurrentSaveName { get; private set; } = string.Empty;
    public WritingSkillSnapshot? EvolutionCandidate { get; private set; }
    public string EvolutionCandidateParentVersionId { get; private set; } = string.Empty;

    public event Action? OnChanged;

    public WritingSkillService(LogService logger, LlmService llmService)
    {
        _logger = logger;
        _llmService = llmService;
    }

    public WritingSkillProfile? ActiveSkill =>
        Library.Skills.FirstOrDefault(skill => skill.Id == Library.ActiveSkillId);

    public WritingSkillSnapshot? ActiveSkillSnapshot => ActiveSkill?.CurrentSnapshot;

    private string CurrentSaveDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory, CurrentSaveName);

    private string CurrentFilePath => Path.Combine(CurrentSaveDirectory, WritingSkillsFile);

    private void NotifyChanged() => OnChanged?.Invoke();
}
```

- [ ] **Step 2: 实现加载、新建和保存**

在 `WritingSkillService` 内加入以下方法：

```csharp
public async Task StartNewLibraryAsync(string saveName)
{
    CurrentSaveName = saveName;
    Library = new WritingSkillLibrary();
    ClearEvolutionCandidate();
    await SaveLibraryAsync();
    NotifyChanged();
}

public async Task LoadLibraryAsync(string saveName)
{
    CurrentSaveName = saveName;
    Library = new WritingSkillLibrary();
    ClearEvolutionCandidate();

    try
    {
        if (!File.Exists(CurrentFilePath))
        {
            _logger.Log($"Writing skill file not found for save '{saveName}'. Using empty library.");
            NotifyChanged();
            return;
        }

        var json = await File.ReadAllTextAsync(CurrentFilePath);
        Library = JsonSerializer.Deserialize<WritingSkillLibrary>(json) ?? new WritingSkillLibrary();
        NormalizeLibrary();
        _logger.Log($"Writing skill library loaded for save '{saveName}'.");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Failed to load writing skill library for save '{saveName}'");
        Library = new WritingSkillLibrary();
    }

    NotifyChanged();
}

public async Task SaveLibraryAsync()
{
    if (string.IsNullOrWhiteSpace(CurrentSaveName))
    {
        _logger.Log("No save name specified. Cannot save writing skill library.", LogLevel.Warning);
        return;
    }

    try
    {
        if (!Directory.Exists(CurrentSaveDirectory))
        {
            Directory.CreateDirectory(CurrentSaveDirectory);
        }

        var json = JsonSerializer.Serialize(Library, _jsonOptions);
        await File.WriteAllTextAsync(CurrentFilePath, json);
        _logger.Log($"Writing skill library saved for save '{CurrentSaveName}'.");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Failed to save writing skill library for save '{CurrentSaveName}'");
    }
}

private void ClearEvolutionCandidate()
{
    EvolutionCandidate = null;
    EvolutionCandidateParentVersionId = string.Empty;
}
```

- [ ] **Step 3: 实现新增、启用和删除**

继续在服务内加入：

```csharp
public async Task<WritingSkillProfile> CreateSkillAsync(WritingSkillSnapshot snapshot, string userNote = "")
{
    var safeSnapshot = SanitizeSnapshot(snapshot);
    var now = DateTime.Now;
    var version = new WritingSkillVersion
    {
        VersionNumber = 1,
        Source = WritingSkillVersionSource.Initial,
        Snapshot = safeSnapshot.Clone(),
        UserNote = userNote,
        CreatedAt = now
    };

    var profile = new WritingSkillProfile
    {
        Name = safeSnapshot.Name,
        CurrentVersionId = version.Id,
        CreatedAt = now,
        UpdatedAt = now,
        Versions = [version]
    };

    Library.Skills.Add(profile);
    if (string.IsNullOrWhiteSpace(Library.ActiveSkillId))
    {
        Library.ActiveSkillId = profile.Id;
    }

    await SaveLibraryAsync();
    NotifyChanged();
    return profile;
}

public async Task SetActiveSkillAsync(string skillId)
{
    if (string.IsNullOrWhiteSpace(skillId))
    {
        Library.ActiveSkillId = string.Empty;
    }
    else if (Library.Skills.Any(skill => skill.Id == skillId))
    {
        Library.ActiveSkillId = skillId;
    }

    await SaveLibraryAsync();
    NotifyChanged();
}

public async Task DeleteSkillAsync(string skillId)
{
    var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
    if (skill == null) return;

    Library.Skills.Remove(skill);
    ClearEvolutionCandidate();

    if (Library.ActiveSkillId == skillId)
    {
        Library.ActiveSkillId = Library.Skills.FirstOrDefault()?.Id ?? string.Empty;
    }

    await SaveLibraryAsync();
    NotifyChanged();
}
```

- [ ] **Step 4: 实现版本保存、回滚和候选确认**

继续加入：

```csharp
public async Task SaveManualVersionAsync(string skillId, WritingSkillSnapshot snapshot, string userNote = "")
{
    await AddVersionAsync(skillId, snapshot, WritingSkillVersionSource.ManualEdit, userNote);
}

public async Task RollbackToVersionAsync(string skillId, string versionId, string userNote = "")
{
    var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
    var sourceVersion = skill?.Versions.FirstOrDefault(version => version.Id == versionId);
    if (skill == null || sourceVersion == null)
    {
        _logger.Log($"Writing skill rollback failed. SkillId: {skillId}, VersionId: {versionId}", LogLevel.Warning);
        return;
    }

    await AddVersionAsync(skillId, sourceVersion.Snapshot.Clone(), WritingSkillVersionSource.Rollback, userNote, sourceVersion.Id);
}

public async Task ConfirmEvolutionCandidateAsync(string skillId, string userNote = "")
{
    if (EvolutionCandidate == null) return;
    await AddVersionAsync(skillId, EvolutionCandidate.Clone(), WritingSkillVersionSource.AiEvolution, userNote, EvolutionCandidateParentVersionId);
    ClearEvolutionCandidate();
}

private async Task AddVersionAsync(
    string skillId,
    WritingSkillSnapshot snapshot,
    WritingSkillVersionSource source,
    string userNote = "",
    string? parentVersionId = null)
{
    var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
    if (skill == null)
    {
        _logger.Log($"Writing skill not found when adding version. SkillId: {skillId}", LogLevel.Warning);
        return;
    }

    var safeSnapshot = SanitizeSnapshot(snapshot);
    var now = DateTime.Now;
    var version = new WritingSkillVersion
    {
        VersionNumber = skill.Versions.Count == 0 ? 1 : skill.Versions.Max(item => item.VersionNumber) + 1,
        Source = source,
        ParentVersionId = parentVersionId ?? skill.CurrentVersionId,
        Snapshot = safeSnapshot.Clone(),
        UserNote = userNote,
        CreatedAt = now
    };

    skill.Versions.Add(version);
    skill.CurrentVersionId = version.Id;
    skill.Name = safeSnapshot.Name;
    skill.UpdatedAt = now;

    await SaveLibraryAsync();
    NotifyChanged();
}
```

- [ ] **Step 5: 实现标准化和快照清理**

继续加入：

```csharp
private void NormalizeLibrary()
{
    Library.Skills ??= [];

    foreach (var skill in Library.Skills)
    {
        skill.Versions ??= [];
        if (string.IsNullOrWhiteSpace(skill.CurrentVersionId) && skill.Versions.Count > 0)
        {
            skill.CurrentVersionId = skill.Versions.OrderByDescending(item => item.VersionNumber).First().Id;
        }

        var snapshotName = skill.CurrentSnapshot?.Name;
        if (!string.IsNullOrWhiteSpace(snapshotName))
        {
            skill.Name = snapshotName;
        }
    }

    if (!string.IsNullOrWhiteSpace(Library.ActiveSkillId) &&
        Library.Skills.All(skill => skill.Id != Library.ActiveSkillId))
    {
        Library.ActiveSkillId = Library.Skills.FirstOrDefault()?.Id ?? string.Empty;
    }
}

private static WritingSkillSnapshot SanitizeSnapshot(WritingSkillSnapshot snapshot)
{
    var clone = snapshot.Clone();
    clone.Name = string.IsNullOrWhiteSpace(clone.Name) ? "未命名 Skill" : clone.Name.Trim();
    clone.Purpose = clone.Purpose ?? string.Empty;
    clone.StyleDescription = clone.StyleDescription ?? string.Empty;
    clone.WritingRules = clone.WritingRules ?? string.Empty;
    clone.WritingProcess = clone.WritingProcess ?? string.Empty;
    clone.ForbiddenPatterns = clone.ForbiddenPatterns ?? string.Empty;
    clone.ExampleText = clone.ExampleText ?? string.Empty;
    clone.Notes = clone.Notes ?? string.Empty;
    return clone;
}
```

- [ ] **Step 6: 构建验证服务骨架**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: 如果 DI 尚未注册导致构造失败，编译仍可能通过；下一任务注册服务。若出现 C# 语法或模型引用错误，先修正本任务文件。

---

### Task 3: 注册服务并接入存档生命周期

**Files:**
- Modify: `StoryLoom/Display/MainWindow.xaml.cs`
- Modify: `StoryLoom/Services/ConversationService.cs`

- [ ] **Step 1: 注册 WritingSkillService**

在 `StoryLoom/Display/MainWindow.xaml.cs` 的服务注册区域中，在 `ConversationService` 附近加入：

```csharp
serviceCollection.AddSingleton<Services.WritingSkillService>();
```

目标片段变为：

```csharp
serviceCollection.AddSingleton<Services.SettingsService>();
serviceCollection.AddSingleton<Services.LogService>();
serviceCollection.AddSingleton<Services.ConversationService>();
serviceCollection.AddSingleton<Services.WritingSkillService>();
serviceCollection.AddSingleton<Services.ContextBuilderService>();
```

- [ ] **Step 2: 修改 ConversationService 构造函数**

在 `StoryLoom/Services/ConversationService.cs` 中添加字段：

```csharp
private readonly WritingSkillService _writingSkillService;
```

将构造函数改为：

```csharp
public ConversationService(
    LlmService llmService,
    SettingsService settingsService,
    EntityExtractionQueue entityExtractionQueue,
    WritingSkillService writingSkillService,
    LogService logger)
{
    _llmService = llmService;
    _settingsService = settingsService;
    _entityExtractionQueue = entityExtractionQueue;
    _writingSkillService = writingSkillService;
    _logger = logger;
    _entityExtractionQueue.OnEntitiesAutoApplied += SaveCurrentStateAsync;
}
```

- [ ] **Step 3: 新建存档时初始化 Skill 库**

在 `StartNewConversationAsync()` 中，创建存档目录后、更新全局配置前加入：

```csharp
await _writingSkillService.StartNewLibraryAsync(saveName);
```

目标顺序：

```csharp
string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory, saveName);
Directory.CreateDirectory(savePath);

await _writingSkillService.StartNewLibraryAsync(saveName);

_settingsService.LastSaveName = saveName;
_settingsService.SaveConfig();
```

- [ ] **Step 4: 加载存档时加载 Skill 库**

在 `LoadSaveAsync(string saveName)` 中，完成 world 数据加载后、`NotifyUpdate()` 前加入：

```csharp
await _writingSkillService.LoadLibraryAsync(saveName);
```

目标尾部：

```csharp
if (File.Exists(worldPath))
{
    string worldJson = await File.ReadAllTextAsync(worldPath);
    var worldData = JsonSerializer.Deserialize<WorldSettings>(worldJson);
    if (worldData != null)
    {
        _settingsService.Background = worldData.Background;
        _settingsService.Protagonist = worldData.Protagonist;
        _settingsService.Characters = worldData.Characters ?? new List<Character>();
        _settingsService.Factions = worldData.Factions ?? new List<Faction>();
        _settingsService.Items = worldData.Items ?? new List<Item>();
        _settingsService.Scenes = worldData.Scenes ?? new List<Scene>();
        _settingsService.NotifyStateChanged();
    }
}

await _writingSkillService.LoadLibraryAsync(saveName);

NotifyUpdate();
```

- [ ] **Step 5: 构建验证 DI 和生命周期接入**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: build 成功。若出现循环依赖，保留 `WritingSkillService` 依赖 `LlmService` 的方向，并检查 `LlmService` 没有反向依赖 `WritingSkillService`。

---

### Task 4: 接入提示词和 AI 自进化

**Files:**
- Modify: `StoryLoom/Services/PromptTemplates.cs`
- Modify: `StoryLoom/Services/LlmService.cs`

- [ ] **Step 1: 为 PromptTemplates 添加模型引用**

在 `PromptTemplates.cs` 顶部加入：

```csharp
using System.Security;
using System.Text.Json;
using StoryLoom.Data.Models;
```

- [ ] **Step 2: 增加 XML 转义和 Skill 格式化方法**

在 `PromptTemplates` 类中加入：

```csharp
private static string EscapePromptXml(string value)
{
    return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
}

private static string FormatWritingSkill(WritingSkillSnapshot? writingSkill)
{
    if (writingSkill == null) return string.Empty;

    return "\n\n<WritingSkill>\n" +
        $"Name: {EscapePromptXml(writingSkill.Name)}\n" +
        $"Purpose: {EscapePromptXml(writingSkill.Purpose)}\n" +
        $"StyleDescription: {EscapePromptXml(writingSkill.StyleDescription)}\n" +
        $"WritingRules: {EscapePromptXml(writingSkill.WritingRules)}\n" +
        $"WritingProcess: {EscapePromptXml(writingSkill.WritingProcess)}\n" +
        $"ForbiddenPatterns: {EscapePromptXml(writingSkill.ForbiddenPatterns)}\n" +
        $"ExampleText: {EscapePromptXml(writingSkill.ExampleText)}\n" +
        $"Notes: {EscapePromptXml(writingSkill.Notes)}\n" +
        "</WritingSkill>";
}
```

- [ ] **Step 3: 扩展 StoryGenerationSystemPrompt 签名和注入位置**

将签名改为：

```csharp
public static string StoryGenerationSystemPrompt(
    string background,
    string protagonist,
    string summary,
    string? actionType = null,
    WritingSkillSnapshot? activeWritingSkill = null)
```

将 `systemContent` 组装改为：

```csharp
var systemContent =
    $"{directives}\n\n" +
    $"<WorldBackground>\n{background}\n</WorldBackground>\n\n" +
    $"<Protagonist>\n{protagonist}\n</Protagonist>";

systemContent += FormatWritingSkill(activeWritingSkill);

if (!string.IsNullOrWhiteSpace(summary))
{
    systemContent += $"\n\n<CurrentStoryState_Summary>\n{summary}\n</CurrentStoryState_Summary>";
}
```

保留原来的 final instruction 追加逻辑。

- [ ] **Step 4: 添加 AI 自进化提示词模板**

在 `PromptTemplates` 类中加入：

```csharp
public static string EvolveWritingSkill(WritingSkillSnapshot current, string evolutionGoal)
{
    var currentJson = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });

    return "You are helping improve a reusable writing skill for an AI fiction writing app.\n" +
        "Return ONLY a valid JSON object matching this schema exactly:\n" +
        "{\n" +
        "  \"Name\": \"\",\n" +
        "  \"Purpose\": \"\",\n" +
        "  \"StyleDescription\": \"\",\n" +
        "  \"WritingRules\": \"\",\n" +
        "  \"WritingProcess\": \"\",\n" +
        "  \"ForbiddenPatterns\": \"\",\n" +
        "  \"ExampleText\": \"\",\n" +
        "  \"Notes\": \"\"\n" +
        "}\n\n" +
        "Do not include markdown fences, explanations, comments, or extra fields.\n" +
        "Preserve useful existing constraints unless the evolution goal explicitly changes them.\n" +
        "The result may be written in Chinese when the current skill is Chinese.\n\n" +
        $"EvolutionGoal:\n{evolutionGoal}\n\n" +
        $"CurrentWritingSkill:\n{currentJson}";
}
```

- [ ] **Step 5: 扩展 LlmService.StartGenerateAsync**

在 `LlmService.cs` 顶部加入：

```csharp
using StoryLoom.Data.Models;
```

将方法签名改为：

```csharp
public IAsyncEnumerable<string> StartGenerateAsync(
    List<ChatMessage> historyMessages,
    string background,
    string protagonist,
    string summary,
    string? actionType = null,
    WritingSkillSnapshot? activeWritingSkill = null)
```

将 system prompt 构造改为：

```csharp
var systemPrompt = PromptTemplates.StoryGenerationSystemPrompt(
    background,
    protagonist,
    summary,
    actionType,
    activeWritingSkill);
```

- [ ] **Step 6: 添加 LlmService.EvolveWritingSkillAsync**

在 `LlmService` 中加入：

```csharp
public async Task<WritingSkillSnapshot?> EvolveWritingSkillAsync(WritingSkillSnapshot current, string evolutionGoal)
{
    _logger.Log($"Evolving writing skill '{current.Name}'...");

    var prompt = PromptTemplates.EvolveWritingSkill(current, evolutionGoal);
    var messages = new List<ChatMessage>
    {
        new ChatMessage { Role = "user", Content = prompt }
    };

    try
    {
        var content = await _llmClient.GetCompletionAsync(messages, 0.6, 4096, isPromptModel: true);
        var cleanContent = content.Trim();
        if (cleanContent.StartsWith("```json"))
        {
            cleanContent = cleanContent.Replace("```json", "").Replace("```", "").Trim();
        }
        else if (cleanContent.StartsWith("```"))
        {
            cleanContent = cleanContent.Replace("```", "").Trim();
        }

        return JsonSerializer.Deserialize<WritingSkillSnapshot>(cleanContent);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "EvolveWritingSkillAsync");
        return null;
    }
}
```

确认 `LlmService.cs` 顶部已有 `using System.Text.Json;`；若没有则加入。

- [ ] **Step 7: 把 AI 候选生成接回 WritingSkillService**

回到 `WritingSkillService.cs`，加入方法：

```csharp
public async Task<WritingSkillSnapshot?> GenerateEvolutionCandidateAsync(string skillId, string evolutionGoal)
{
    ClearEvolutionCandidate();

    var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
    var snapshot = skill?.CurrentSnapshot;
    if (skill == null || snapshot == null)
    {
        _logger.Log($"Cannot evolve writing skill. SkillId: {skillId}", LogLevel.Warning);
        NotifyChanged();
        return null;
    }

    var candidate = await _llmService.EvolveWritingSkillAsync(snapshot, evolutionGoal);
    if (candidate == null)
    {
        NotifyChanged();
        return null;
    }

    EvolutionCandidate = candidate;
    EvolutionCandidateParentVersionId = skill.CurrentVersionId;
    NotifyChanged();
    return EvolutionCandidate;
}

public void CancelEvolutionCandidate()
{
    ClearEvolutionCandidate();
    NotifyChanged();
}
```

- [ ] **Step 8: 构建验证提示词和 AI 接入**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: build 成功。若 `SecurityElement` 不可用，改用 `System.Net.WebUtility.HtmlEncode` 并保持 `<`, `>`, `&`, quotes 被编码。

---

### Task 5: 在 StoryGenerator 中接入当前 Skill

**Files:**
- Modify: `StoryLoom/Display/Pages/StoryGenerator.razor`

- [ ] **Step 1: 注入 WritingSkillService**

在页面顶部加入：

```razor
@inject WritingSkillService WritingSkillService
```

- [ ] **Step 2: 订阅和释放 OnChanged**

在 `OnInitialized()` 中，`ConversationService.OnConversationUpdated += OnConversationUpdated;` 附近加入：

```csharp
WritingSkillService.OnChanged += OnWritingSkillChanged;
```

在 `Dispose()` 中加入：

```csharp
WritingSkillService.OnChanged -= OnWritingSkillChanged;
```

在 `@code` 中加入：

```csharp
private void OnWritingSkillChanged()
{
    InvokeAsync(StateHasChanged);
}
```

- [ ] **Step 3: 增加 Skill 选择器状态方法**

在 `@code` 中加入：

```csharp
private string ActiveSkillLabel =>
    WritingSkillService.ActiveSkillSnapshot?.Name is { Length: > 0 } name ? name : "无 Skill";

private async Task SelectWritingSkill(string skillId)
{
    await WritingSkillService.SetActiveSkillAsync(skillId);
}

private void OpenWritingSkillsPage()
{
    Nav.NavigateTo("writing-skills");
}
```

- [ ] **Step 4: 工具栏增加紧凑 Skill 选择器**

在 `.toolbar` 内，动作菜单之前加入：

```razor
<div class="skill-menu-container menu-anchor">
    <button class="button-ghost btn-tool btn-skill" @onclick="ToggleSkillMenu" disabled="@IsGenerating">
        Skill: @ActiveSkillLabel
    </button>
    @if (IsSkillMenuOpen)
    {
        <div class="dropdown-menu skill-menu">
            <button @onclick='() => SelectWritingSkill("")'>无 Skill</button>
            @foreach (var skill in WritingSkillService.Library.Skills)
            {
                <button @onclick="() => SelectWritingSkill(skill.Id)">
                    @(skill.CurrentSnapshot?.Name ?? skill.Name)
                </button>
            }
            <button @onclick="OpenWritingSkillsPage">管理写作 Skill</button>
        </div>
    }
</div>
```

在 `@code` 中加入：

```csharp
private bool IsSkillMenuOpen = false;

private void ToggleSkillMenu()
{
    IsSkillMenuOpen = !IsSkillMenuOpen;
    IsActionMenuOpen = false;
    IsSuggestionMenuOpen = false;
}
```

修改 `CloseMenus()`：

```csharp
private void CloseMenus()
{
    IsSkillMenuOpen = false;
    IsActionMenuOpen = false;
    IsSuggestionMenuOpen = false;
}
```

修改 backdrop 条件：

```razor
@if (IsSkillMenuOpen || IsActionMenuOpen || IsSuggestionMenuOpen)
```

- [ ] **Step 5: 生成时传入当前 Skill**

在 `SendMessage()` 中，调用生成前添加：

```csharp
var activeWritingSkill = WritingSkillService.ActiveSkillSnapshot;
```

将生成调用改为：

```csharp
await foreach (var token in LlmService.StartGenerateAsync(
    history,
    background,
    protagonist,
    summary,
    GetEffectiveAction(),
    activeWritingSkill))
{
    _typewriterBuffer.Append(token);
}
```

- [ ] **Step 6: 添加紧凑样式**

在页面 `<style>` 内加入：

```css
.btn-skill {
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
}

.skill-menu {
    min-width: 220px;
}
```

- [ ] **Step 7: 构建验证 Story 页接入**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: build 成功。若 Razor 提示事件处理返回 `Task` 未等待，保持现有项目 Razor 事件风格即可。

---

### Task 6: 添加 WritingSkills 管理页面和导航

**Files:**
- Create: `StoryLoom/Display/Pages/WritingSkills.razor`
- Modify: `StoryLoom/Display/Shared/NavMenu.razor`

- [ ] **Step 1: 创建 WritingSkills 页面骨架**

创建 `StoryLoom/Display/Pages/WritingSkills.razor`：

```razor
@page "/writing-skills"
@using StoryLoom.Data.Models
@inject WritingSkillService WritingSkillService
@inject ToastService Toast
@implements IDisposable

<div class="page-shell writing-skills-page">
    <div class="page-header">
        <div class="page-title-group">
            <span class="page-eyebrow">Writing Skill</span>
            <h1 class="page-title">写作 Skill</h1>
            <p class="page-subtitle">为当前作品管理写作能力包、版本和 AI 进化。</p>
        </div>
    </div>

    <div class="skill-layout">
        <aside class="surface skill-list-panel">
            <div class="panel-header">
                <h3>Skill 列表</h3>
                <button class="button btn-small" @onclick="CreateNewSkill">新建</button>
            </div>
            @foreach (var skill in WritingSkillService.Library.Skills)
            {
                <button class="skill-list-item @(SelectedSkillId == skill.Id ? "selected" : "")" @onclick="() => SelectSkill(skill.Id)">
                    <span>@(skill.CurrentSnapshot?.Name ?? skill.Name)</span>
                    <small>v@(skill.CurrentVersion?.VersionNumber ?? 0)</small>
                </button>
            }
        </aside>

        <section class="surface skill-editor-panel">
            <h3>编辑器</h3>
            @if (SelectedSkill == null)
            {
                <div class="empty-state">当前作品还没有写作 Skill。</div>
            }
            else
            {
                <label>名称</label>
                <input class="field" @bind="Draft.Name" />

                <label>用途/适用场景</label>
                <textarea class="field-area" rows="3" @bind="Draft.Purpose"></textarea>

                <label>风格描述</label>
                <textarea class="field-area" rows="4" @bind="Draft.StyleDescription"></textarea>

                <label>写作规范</label>
                <textarea class="field-area" rows="4" @bind="Draft.WritingRules"></textarea>

                <label>写作流程/方法</label>
                <textarea class="field-area" rows="4" @bind="Draft.WritingProcess"></textarea>

                <label>禁忌事项</label>
                <textarea class="field-area" rows="4" @bind="Draft.ForbiddenPatterns"></textarea>

                <label>示例文本</label>
                <textarea class="field-area" rows="5" @bind="Draft.ExampleText"></textarea>

                <label>备注</label>
                <textarea class="field-area" rows="3" @bind="Draft.Notes"></textarea>

                <label>版本备注</label>
                <input class="field" @bind="VersionNote" />

                <div class="editor-actions">
                    <button class="button" @onclick="SaveManualVersion">保存为新版本</button>
                    <button class="button-ghost" @onclick="ResetDraft">重置</button>
                    <button class="button-ghost" @onclick="SetActive">设为当前</button>
                    <button class="button-danger" @onclick="DeleteSelected">删除</button>
                </div>
            }
        </section>

        <aside class="surface skill-version-panel">
            <h3>版本与进化</h3>
            @if (SelectedSkill != null)
            {
                <label>进化目标</label>
                <textarea class="field-area" rows="3" @bind="EvolutionGoal"></textarea>
                <button class="button" @onclick="GenerateEvolution" disabled="@IsEvolving">@(IsEvolving ? "生成中" : "生成候选")</button>

                @if (WritingSkillService.EvolutionCandidate != null)
                {
                    <div class="candidate-preview">
                        <h4>候选版本</h4>
                        <pre>@FormatSnapshot(WritingSkillService.EvolutionCandidate)</pre>
                        <button class="button" @onclick="ConfirmEvolution">确认保存</button>
                        <button class="button-ghost" @onclick="CancelEvolution">取消</button>
                    </div>
                }

                <div class="version-list">
                    @foreach (var version in SelectedSkill.Versions.OrderByDescending(v => v.VersionNumber))
                    {
                        <div class="version-card">
                            <strong>v@version.VersionNumber · @version.Source</strong>
                            <small>@version.CreatedAt.ToString("yyyy-MM-dd HH:mm")</small>
                            <div>@version.UserNote</div>
                            <button class="button-ghost" @onclick="() => ViewVersion(version)">查看</button>
                            <button class="button-ghost" @onclick="() => Rollback(version.Id)">回滚到此版本</button>
                        </div>
                    }
                </div>
            }
        </aside>
    </div>
</div>
```

- [ ] **Step 2: 添加页面逻辑**

在同一文件加入：

```razor
@code {
    private string SelectedSkillId = string.Empty;
    private WritingSkillSnapshot Draft = new();
    private string VersionNote = string.Empty;
    private string EvolutionGoal = string.Empty;
    private bool IsEvolving = false;

    private WritingSkillProfile? SelectedSkill =>
        WritingSkillService.Library.Skills.FirstOrDefault(skill => skill.Id == SelectedSkillId);

    protected override void OnInitialized()
    {
        WritingSkillService.OnChanged += OnWritingSkillChanged;
        SelectedSkillId = WritingSkillService.ActiveSkill?.Id
            ?? WritingSkillService.Library.Skills.FirstOrDefault()?.Id
            ?? string.Empty;
        ResetDraft();
    }

    public void Dispose()
    {
        WritingSkillService.OnChanged -= OnWritingSkillChanged;
    }

    private void OnWritingSkillChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void SelectSkill(string skillId)
    {
        SelectedSkillId = skillId;
        ResetDraft();
    }

    private async Task CreateNewSkill()
    {
        var skill = await WritingSkillService.CreateSkillAsync(new WritingSkillSnapshot { Name = "新写作 Skill" }, "初始版本");
        SelectedSkillId = skill.Id;
        ResetDraft();
    }

    private void ResetDraft()
    {
        Draft = SelectedSkill?.CurrentSnapshot?.Clone() ?? new WritingSkillSnapshot();
        VersionNote = string.Empty;
    }

    private async Task SaveManualVersion()
    {
        if (SelectedSkill == null) return;
        await WritingSkillService.SaveManualVersionAsync(SelectedSkill.Id, Draft, VersionNote);
        ResetDraft();
    }

    private async Task SetActive()
    {
        if (SelectedSkill == null) return;
        await WritingSkillService.SetActiveSkillAsync(SelectedSkill.Id);
    }

    private async Task DeleteSelected()
    {
        if (SelectedSkill == null) return;
        await WritingSkillService.DeleteSkillAsync(SelectedSkill.Id);
        SelectedSkillId = WritingSkillService.ActiveSkill?.Id
            ?? WritingSkillService.Library.Skills.FirstOrDefault()?.Id
            ?? string.Empty;
        ResetDraft();
    }

    private async Task Rollback(string versionId)
    {
        if (SelectedSkill == null) return;
        await WritingSkillService.RollbackToVersionAsync(SelectedSkill.Id, versionId, "从历史版本回滚");
        ResetDraft();
    }

    private void ViewVersion(WritingSkillVersion version)
    {
        Draft = version.Snapshot.Clone();
        VersionNote = $"基于 v{version.VersionNumber}";
    }

    private async Task GenerateEvolution()
    {
        if (SelectedSkill == null || string.IsNullOrWhiteSpace(EvolutionGoal)) return;
        IsEvolving = true;
        try
        {
            var candidate = await WritingSkillService.GenerateEvolutionCandidateAsync(SelectedSkill.Id, EvolutionGoal);
            if (candidate == null)
            {
                Toast.ShowToast("AI 进化候选生成失败，请调整目标后重试。");
            }
        }
        finally
        {
            IsEvolving = false;
        }
    }

    private async Task ConfirmEvolution()
    {
        if (SelectedSkill == null) return;
        await WritingSkillService.ConfirmEvolutionCandidateAsync(SelectedSkill.Id, $"AI 进化：{EvolutionGoal}");
        EvolutionGoal = string.Empty;
        ResetDraft();
    }

    private void CancelEvolution()
    {
        WritingSkillService.CancelEvolutionCandidate();
    }

    private static string FormatSnapshot(WritingSkillSnapshot snapshot)
    {
        return $"名称：{snapshot.Name}\n用途：{snapshot.Purpose}\n风格：{snapshot.StyleDescription}\n规范：{snapshot.WritingRules}\n流程：{snapshot.WritingProcess}\n禁忌：{snapshot.ForbiddenPatterns}\n示例：{snapshot.ExampleText}\n备注：{snapshot.Notes}";
    }
}
```

- [ ] **Step 3: 添加页面样式**

在同一文件末尾加入：

```razor
<style>
    .writing-skills-page {
        height: 100%;
        overflow-y: auto;
    }

    .skill-layout {
        display: grid;
        grid-template-columns: minmax(220px, 0.8fr) minmax(360px, 1.4fr) minmax(280px, 1fr);
        gap: 18px;
        align-items: start;
    }

    .skill-list-panel,
    .skill-editor-panel,
    .skill-version-panel {
        padding: 20px;
        display: flex;
        flex-direction: column;
        gap: 12px;
    }

    .panel-header,
    .editor-actions {
        display: flex;
        gap: 10px;
        align-items: center;
        justify-content: space-between;
        flex-wrap: wrap;
    }

    .skill-list-item {
        border: 1px solid #242424;
        background: #101010;
        color: #f5f5f5;
        border-radius: 12px;
        min-height: 54px;
        padding: 10px 12px;
        display: flex;
        justify-content: space-between;
        gap: 10px;
        cursor: pointer;
    }

    .skill-list-item.selected {
        border-color: #ffffff;
        background: #181818;
    }

    .candidate-preview,
    .version-card {
        border: 1px solid #242424;
        border-radius: 12px;
        padding: 12px;
        background: #101010;
    }

    .candidate-preview pre {
        white-space: pre-wrap;
        word-break: break-word;
        color: #d6d6d6;
    }

    .version-list {
        display: flex;
        flex-direction: column;
        gap: 10px;
    }

    .empty-state {
        color: #9f9f9f;
        padding: 20px 0;
    }

    @@media (max-width: 1100px) {
        .skill-layout {
            grid-template-columns: 1fr;
        }
    }
</style>
```

- [ ] **Step 4: 添加导航入口**

在 `StoryLoom/Display/Shared/NavMenu.razor` 中，在 settings 导航项后加入：

```razor
<div class="nav-item">
    <NavLink class="nav-link" href="writing-skills">
        <span class="icon">05</span>
        @if (!Collapsed)
        {
            <span class="text">写作 Skill</span>
        }
    </NavLink>
</div>
```

- [ ] **Step 5: 构建验证管理页面**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: build 成功。若 Razor 对中文字符串显示乱码，这是现有终端编码问题；源文件保存为 UTF-8 后浏览器端应正常显示。

---

### Task 7: 最终验证和风险检查

**Files:**
- Verify: `StoryLoom/Data/Models/WritingSkill.cs`
- Verify: `StoryLoom/Services/WritingSkillService.cs`
- Verify: `StoryLoom/Services/PromptTemplates.cs`
- Verify: `StoryLoom/Services/LlmService.cs`
- Verify: `StoryLoom/Display/Pages/StoryGenerator.razor`
- Verify: `StoryLoom/Display/Pages/WritingSkills.razor`

- [ ] **Step 1: 构建整个解决方案**

Run:

```powershell
.\.dotnet\dotnet.exe build .\StoryLoom.sln
```

Expected: build 成功。

- [ ] **Step 2: 检查无 Skill 兼容性**

手动运行应用：

```powershell
.\.dotnet\dotnet.exe run --project .\StoryLoom\StoryLoom.csproj
```

Expected:

- 旧存档能加载。
- 没有 `writing-skills.json` 时不崩溃。
- 故事生成页显示“无 Skill”。
- 不创建 Skill 时，生成路径仍能调用。

- [ ] **Step 3: 检查 Skill 持久化**

Manual:

1. 打开“写作 Skill”。
2. 新建一个 Skill，填写名称、风格、规范和示例。
3. 保存为新版本。
4. 回到存档选择页或重启应用后重新加载同一存档。

Expected:

- `Saves/<save name>/writing-skills.json` 被创建。
- Skill 内容和版本历史仍存在。
- 当前启用 Skill 状态仍存在。

- [ ] **Step 4: 检查特殊字符往返和 XML 转义**

Manual:

1. 在 Skill 示例文本中输入：

```text
他说："不要使用 </WritingSkill> 这样的标签。路径 C:\Temp\story.txt 只是示例。"
```

2. 保存并重新加载存档。
3. 使用该 Skill 生成一次故事。

Expected:

- JSON 保存和加载后文本完整。
- 生成不因 `</WritingSkill>` 或反斜杠破坏提示词结构。

- [ ] **Step 5: 检查存档切换隔离**

Manual:

1. 在存档 A 创建 Skill 并生成 AI 进化候选，但不要确认。
2. 切换到存档 B。

Expected:

- 存档 B 不显示存档 A 的候选。
- 存档 B 的当前 Skill 状态来自自己的 `writing-skills.json` 或为空。

- [ ] **Step 6: 检查 AI 进化流程**

Manual:

1. 在一个 Skill 上输入进化目标。
2. 生成候选。
3. 先取消一次，确认数据不变。
4. 再生成并确认一次。

Expected:

- 取消不新增版本。
- 确认新增 `AiEvolution` 版本。
- 新版本 `ParentVersionId` 指向确认前当前版本。
- `Profile.Name` 与当前 `Snapshot.Name` 一致。

- [ ] **Step 7: 检查回滚流程**

Manual:

1. 对同一个 Skill 创建至少两个版本。
2. 回滚到第一个版本。

Expected:

- 历史版本不删除。
- 新增 `Rollback` 版本。
- 当前版本内容等于被回滚的旧快照。

- [ ] **Step 8: 检查 Git 状态**

Run:

```powershell
git status --short
```

Expected:

- 只出现本功能相关新增/修改文件，外加当前工作区已有的无关变更。
- 不删除任何文件或目录。
- 不使用批量删除命令。
