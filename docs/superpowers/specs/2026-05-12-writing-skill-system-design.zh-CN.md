# StoryLoom 写作 Skill 系统设计

## 目标

为 StoryLoom 构建应用内的写作 Skill 系统。写作 Skill 是一个可复用的写作能力包，隶属于单个故事存档。它通过用户定义的风格、规则、流程、禁忌、示例和备注来指导故事生成。

第一版支持：每次生成使用一个当前启用的 Skill、每个存档保存多个 Skill、手动编辑、AI 辅助进化，以及版本历史。

## 已确认范围

- Skill 按故事存档保存，不做全局共享。
- 每个存档目录使用独立的 `writing-skills.json` 文件。
- 支持多个 Writing Skill，但生成时只使用一个当前启用的 Skill。
- Skill 使用完整结构：
  - 名称
  - 用途/适用场景
  - 风格描述
  - 写作规范
  - 写作流程/方法
  - 禁忌事项
  - 示例文本
  - 备注
- 手动编辑保存为新版本。
- AI 根据当前 Skill 和用户提供的进化目标生成完整新版 Skill 快照。
- AI 进化结果必须由用户确认后才保存。
- 保留版本历史和父版本关系。
- 回滚通过旧快照创建新的回滚版本，不覆盖历史。

第一版不做：

- 全局 Skill 库。
- 跨存档共享 Skill。
- 多个 Skill 同时启用。
- 根据行动类型自动选择 Skill。
- 批量删除。
- 未经用户确认的自动进化。
- 在存档列表中展示 Skill 元数据。

## 数据模型

新增模型文件：

`StoryLoom/Data/Models/WritingSkill.cs`

```csharp
public class WritingSkillLibrary
{
    public int SaveVersion { get; set; } = 1;
    public string ActiveSkillId { get; set; } = "";
    public List<WritingSkillProfile> Skills { get; set; } = new();
}

public class WritingSkillProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string CurrentVersionId { get; set; } = "";
    public List<WritingSkillVersion> Versions { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class WritingSkillVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int VersionNumber { get; set; }
    public WritingSkillVersionSource Source { get; set; }
    public string ParentVersionId { get; set; } = "";
    public WritingSkillSnapshot Snapshot { get; set; } = new();
    public string UserNote { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class WritingSkillSnapshot
{
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string StyleDescription { get; set; } = "";
    public string WritingRules { get; set; } = "";
    public string WritingProcess { get; set; } = "";
    public string ForbiddenPatterns { get; set; } = "";
    public string ExampleText { get; set; } = "";
    public string Notes { get; set; } = "";
}

public enum WritingSkillVersionSource
{
    Initial,
    ManualEdit,
    AiEvolution,
    Rollback
}
```

设计要点：

- `WritingSkillProfile` 表示一个 Skill 的身份。
- `WritingSkillProfile.Name` 作为显示/缓存字段保留，必须在每次创建新版本时同步为当前版本的 `Snapshot.Name`。
- `WritingSkillVersion` 表示该 Skill 的某个历史版本。
- 当前生效内容始终来自当前版本里的 `Snapshot`。
- AI 进化和回滚都不覆盖历史。
- `WritingSkillProfile.UpdatedAt` 只在创建新版本时更新。切换当前启用 Skill 不更新该时间戳。

## 存储方式

每个存档目录新增一个 Skill 文件：

```text
Saves/<save name>/writing-skills.json
```

旧存档没有该文件时仍然正常加载。没有 Skill 文件时，应用在内存中创建空的 `WritingSkillLibrary`；只有当用户修改 Skill 数据后才写入文件。

## 服务设计

新增服务：

`StoryLoom/Services/WritingSkillService.cs`

职责：

- 跟踪当前存档名。
- 加载和保存 `writing-skills.json`。
- 暴露当前 `WritingSkillLibrary`。
- 暴露当前启用的 Skill 和当前启用的 Skill 快照。
- 创建新 Skill。
- 删除一个明确指定的 Skill。
- 设置当前启用 Skill。
- 将手动编辑保存为新版本。
- 基于历史快照创建新的回滚版本。
- 生成 AI 进化候选。
- 将 AI 进化候选确认为新版本。
- 变更后触发 `OnChanged` 事件。

生命周期与事件消费者：

- 在 `MainWindow.xaml.cs` 中将 `WritingSkillService` 注册为 Singleton。
- `WritingSkills.razor` 订阅 `OnChanged`，用于刷新 Skill 列表、编辑器、版本列表和候选预览。
- `StoryGenerator.razor` 订阅 `OnChanged`，用于刷新紧凑的当前 Skill 选择器。
- 两个 Razor 组件都必须在释放时取消订阅。

与 `ConversationService` 的协作：

- `StartNewConversationAsync()` 创建存档目录后调用 `WritingSkillService.StartNewLibraryAsync(saveName)`。
- `LoadSaveAsync(saveName)` 加载聊天和世界数据后调用 `WritingSkillService.LoadLibraryAsync(saveName)`。
- `SaveCurrentStateAsync()` 不负责保存 Skill 数据。
- `GetAvailableSavesAsync()` 第一版不读取 Skill 数据。

默认行为：

- 没有当前启用 Skill 时，生成逻辑与当前项目保持一致。
- 创建第一个 Skill 时自动设为当前启用 Skill。
- 删除当前启用 Skill 时，自动选择剩余 Skill 中的第一个；如果没有剩余 Skill，则清空当前启用状态。
- 加载 Skill 库、新建 Skill 库、删除 Skill、生成新的 AI 候选时，应在合适时机清理旧的 AI 进化候选状态，避免候选内容跨存档或跨 Skill 残留。

## 提示词接入

扩展生成链路：

```text
StoryGenerator.razor
-> WritingSkillService.ActiveSkillSnapshot
-> LlmService.StartGenerateAsync(..., activeWritingSkill)
-> PromptTemplates.StoryGenerationSystemPrompt(..., activeWritingSkill)
```

如果没有当前启用 Skill，不注入任何 Skill 内容。

如果存在当前启用 Skill，在系统提示词中注入 `WritingSkill` 区块：

```text
<WritingSkill>
Name:
Purpose:
StyleDescription:
WritingRules:
WritingProcess:
ForbiddenPatterns:
ExampleText:
Notes:
</WritingSkill>
```

注入前必须对所有 Skill 字段做 XML 转义。用户文本可能包含 `</WritingSkill>`、引号、`&`、反斜杠或 Windows 路径；这些内容必须作为普通文本保留，不能破坏提示词结构。

`StartGenerateAsync` 通过新增 nullable 默认参数保持向后兼容：

```csharp
public IAsyncEnumerable<string> StartGenerateAsync(
    List<ChatMessage> historyMessages,
    string background,
    string protagonist,
    string summary,
    string? actionType = null,
    WritingSkillSnapshot? activeWritingSkill = null)
```

`<WritingSkill>` 插入在 `<Protagonist>` 之后、`<CurrentStoryState_Summary>` 之前：

```text
directives
<WorldBackground>
<Protagonist>
<WritingSkill>
<CurrentStoryState_Summary>
final instruction
```

提示词优先级：

- 世界背景、主角设定和故事连续性仍然决定事实内容。
- 写作规范、禁忌事项和写作流程决定文本写法。
- 风格描述和示例文本用于引导语气、节奏和文风。
- 已选择的行动类型仍然约束当前生成片段的动作方向。
- 模型仍然只能输出纯故事正文，不添加解释、分析或选项。

## AI 自进化

新增提示词模板，例如：

```csharp
public static string EvolveWritingSkill(WritingSkillSnapshot current, string evolutionGoal)
```

该提示词要求模型输出完整的 `WritingSkillSnapshot` JSON 对象。即使某个字段没有变化，也必须完整返回。

流程：

1. 用户选择一个 Skill。
2. 用户输入进化目标。
3. `WritingSkillService` 调用提示词模型生成候选快照。
4. 服务将 JSON 解析成 `WritingSkillSnapshot`。
5. UI 展示候选内容。
6. 用户确认或取消。
7. 确认后创建 `AiEvolution` 版本，并将 `ParentVersionId` 设为进化前版本。
8. 取消则不修改任何数据。

模型与校验边界：

- 使用配置中的提示词模型执行进化。
- 输出预算需要足够容纳完整 JSON。第一版使用 `maxTokens = 4096`。
- 服务只校验响应能否解析为 `WritingSkillSnapshot`。
- 内容质量由用户在预览阶段判断。第一版不自动校验规则是否矛盾、示例是否薄弱、风格指导是否足够好。
- 解析失败或候选为空时，当前 Skill 不发生变化。

## UI 设计

新增页面：

`StoryLoom/Display/Pages/WritingSkills.razor`

导航：

- 在应用导航中加入“写作 Skill”入口，编号为 `05`，放在现有 `01` 到 `04` 之后。不重排已有导航编号。

布局：

- 左栏：Skill 列表。
- 中栏：Skill 编辑器。
- 右栏：版本历史和 AI 进化控制区。

Skill 列表操作：

- 新建 Skill。
- 选择 Skill。
- 设为当前启用 Skill。
- 删除当前选中的明确 Skill。

编辑器字段：

- 名称。
- 用途/适用场景。
- 风格描述。
- 写作规范。
- 写作流程/方法。
- 禁忌事项。
- 示例文本。
- 备注。
- 版本备注。

编辑器操作：

- 保存为新版本。
- 重置未保存修改。

版本操作：

- 查看版本快照。
- 回滚到该版本。

AI 进化操作：

- 输入进化目标。
- 生成候选。
- 预览候选的完整字段。
- 确认候选为新版本。
- 取消候选。

故事生成页：

- 在工具栏增加紧凑的 Skill 选择控件。
- 包含“无 Skill”选项。
- 提供跳转到管理页面的入口。

## 错误处理

- `writing-skills.json` 无法读取或格式无效：记录日志，并加载空库，保证存档仍可使用。
- `writing-skills.json` 写入失败：捕获异常、记录日志，并保持应用可用。不要从 UI 触发的保存路径向外抛出异常。
- AI 进化 JSON 解析失败：显示提示，不修改当前 Skill。
- 找不到所选版本：记录日志，保持当前版本不变。
- Skill 库为空：故事生成继续执行，不注入 Skill。
- 存档切换：加载下一个存档的 Skill 库前，清理未确认的 AI 进化候选。

## 测试计划

服务层或单元级检查：

- 新库初始为空。
- 第一个 Skill 自动成为当前启用 Skill。
- 手动编辑会创建新版本。
- AI 进化确认后会创建新版本，并写入正确父版本。
- 回滚会创建新的回滚版本，不删除历史。
- 删除当前启用 Skill 后，当前启用状态能安全更新。
- 加载没有 `writing-skills.json` 的旧存档不会失败。
- 切换到其他存档时，会清理未确认的 AI 进化候选。
- Snapshot 文本包含引号、反斜杠、`&` 和类似 `</WritingSkill>` 的 XML 字符串时，保存和重新加载后内容完整。
- Skill 注入系统提示词前，会转义 XML 敏感字符。

集成检查：

- 项目能成功构建。
- 没有当前启用 Skill 时，既有生成逻辑正常工作。
- 有当前启用 Skill 时，生成提示词包含 Skill 内容。
- Skill 数据能在存档重新加载后保留。
- 存档切换后，当前 Skill 和候选状态只来自当前存档。
- 写入失败会记录日志，不让 UI 路径崩溃。

手动 UI 检查：

- 创建 Skill。
- 切换当前启用 Skill。
- 编辑并保存 Skill。
- 生成 AI 进化候选。
- 确认和取消候选。
- 查看版本并回滚。

## 实现阶段

第一阶段：模型和持久化

- 添加 `WritingSkill` 模型类。
- 添加 `WritingSkillService`。
- 将 `WritingSkillService` 以 Singleton 方式接入依赖注入。
- 在 `ConversationService` 中接入存档新建和加载流程。

第二阶段：提示词接入

- 扩展 `LlmService.StartGenerateAsync`，新增 `WritingSkillSnapshot? activeWritingSkill = null` 参数。
- 扩展 `PromptTemplates.StoryGenerationSystemPrompt`。
- 添加 `PromptTemplates.EvolveWritingSkill`。
- 在 `StoryGenerator.razor` 注入 `WritingSkillService`。
- 生成时读取 `WritingSkillService.ActiveSkillSnapshot`。
- 将当前快照传入 `LlmService.StartGenerateAsync`。

第三阶段：管理 UI

- 添加 `WritingSkills.razor`。
- 添加导航入口。
- 实现 Skill 列表、编辑器、版本历史、回滚和 AI 进化预览。

第四阶段：故事页选择器

- 在 `StoryGenerator.razor` 中添加紧凑的当前 Skill 选择控件。
- 包含“无 Skill”和跳转管理页入口。

第五阶段：验证和打磨

- 运行构建。
- 测试存档加载和保存。
- 测试无 Skill 兼容性。
- 测试启用 Skill 后的生成。
- 根据第一次使用体验调整 UI 文案和布局。
