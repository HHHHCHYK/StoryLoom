# StoryLoom

[English](./README.md)

StoryLoom 是一个 Windows 桌面端 AI 写作工具。它把世界观管理、角色资料、故事续写和本地存档放在一起，适合用来写互动小说、长篇故事草稿，或者给一个正在成形的世界观找灵感。

它不是那种输入一句话就生成整篇小说的工具。StoryLoom 更像一个创作工作台：你先整理世界背景和主角设定，再一轮一轮推进剧情。AI 负责续写、润色和给建议，你负责掌舵。

## 主要功能

- 世界构建：维护故事背景、主角、角色、势力、物品和场景。
- AI 续写：根据当前设定、上下文摘要和最近对话生成后续剧情。
- 写作模式：支持自动、行动、对话、思考等生成方向。
- 输入润色：把用户输入整理成更适合投喂给模型的写作指令。
- 剧情建议：根据当前故事上下文生成下一步可选方向。
- 上下文摘要：当故事变长后，自动把旧内容压缩成摘要，减少模型遗忘。
- 本地存档：故事聊天记录和世界设定会保存到本地，方便继续创作。
- 模型分工：正文模型负责故事生成，提示模型负责总结、建议和润色。

## 技术栈

- .NET 10
- C#
- WPF
- Blazor WebView
- Razor Components
- OpenAI 风格的 Chat Completions API
- Microsoft ML Tokenizers
- Microsoft Semantic Kernel 相关依赖

项目入口是 WPF 窗口，界面主体运行在 Blazor WebView 里。这样既保留了桌面应用的形态，也能用 Razor 写比较灵活的页面。

## 项目结构

```text
StoryLoom/
├─ Agents/              # 智能体相关代码，目前更像后续扩展区
├─ Data/Models/         # 角色、势力、物品、场景等数据模型
├─ Display/             # WPF 和 Blazor UI
│  ├─ Pages/            # 存档、故事生成、世界构建、模型设置页面
│  └─ Shared/           # 布局、导航、Toast 等通用组件
├─ Services/            # 模型调用、设置、存档、日志、提示词等业务服务
├─ wwwroot/             # Blazor WebView 静态资源
└─ StoryLoom.csproj
```

几个值得先看的文件：

- `Display/Pages/WorldBuilding.razor`：世界构建页面。
- `Display/Pages/StoryGenerator.razor`：故事生成页面。
- `Display/Pages/ModelSettings.razor`：模型配置页面。
- `Services/LlmClient.cs`：底层模型请求封装。
- `Services/LlmService.cs`：AI 生成、润色、总结和建议的业务编排。
- `Services/ConversationService.cs`：会话、存档和上下文摘要。
- `Services/PromptTemplates.cs`：提示词模板集中管理。

## 运行环境

你需要准备：

- Windows
- .NET 10 SDK
- 一个兼容 OpenAI Chat Completions 格式的模型服务
- 对应的 API Key

默认模型配置偏向 DeepSeek：

```text
模型名称：deepseek-chat
API 地址：https://api.deepseek.com/v1
```

也可以换成其他兼容接口，只要它支持 `/chat/completions` 这类调用格式。

## 本地运行

在项目根目录执行：

```powershell
dotnet restore .\StoryLoom.sln
dotnet build .\StoryLoom.sln
dotnet run --project .\StoryLoom\StoryLoom.csproj
```

如果你使用 Visual Studio，也可以直接打开 `StoryLoom.sln`，选择 StoryLoom 项目运行。

## 基本使用流程

1. 打开应用后，先进入「模型设置」。
2. 填写正文模型、提示模型和全局 API Key。
3. 分别测试正文模型和提示模型连接。
4. 进入「世界构建」，写下世界背景和主角设定。
5. 按需要补充角色、势力、物品和场景。
6. 进入「故事构建」，输入下一步剧情、对白或创作指令。
7. 使用「建议」「润色」「模式」这些工具辅助推进故事。

如果背景和主角还没写好，故事生成页面会引导你先回到世界构建页面。

## 数据保存

应用会在运行目录下保存配置和存档：

```text
Config/config.json
Saves/<存档名>/chat.json
Saves/<存档名>/world.json
```

其中：

- `config.json` 保存模型设置、温度、上下文窗口、打字机速度等全局配置。
- `chat.json` 保存当前故事的对话和摘要。
- `world.json` 保存世界背景、主角和实体资料。

注意不要把包含 API Key 的本地配置文件提交到公开仓库。

## 当前状态

StoryLoom 已经具备一个 AI 写作桌面应用的基本闭环：配置模型、搭建世界、生成故事、总结上下文、保存进度。Agents 目录和 Semantic Kernel 依赖说明项目可能会继续往多智能体写作协作方向扩展，比如导演智能体、势力智能体、角色智能体等。

## 开发提示

- UI 主要在 `Display/Pages` 和 `Display/Shared` 下。
- 业务状态大多由单例服务管理，例如 `SettingsService` 和 `ConversationService`。
- 模型请求走 `LlmClient`，不要在页面里直接拼 HTTP 请求。
- 提示词集中放在 `PromptTemplates`，修改生成风格时优先看这里。
- 长上下文处理在 `ConversationService.CheckAndSummarizeAsync` 附近。

## 许可证

当前仓库没有看到许可证文件。如果要公开发布，建议补充一个明确的 License。
