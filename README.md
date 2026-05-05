# StoryLoom

[中文](./README.zh-CN.md)

StoryLoom is a Windows desktop app for AI-assisted fiction writing. It keeps worldbuilding notes, character data, story generation, and local saves in one place. It works well for interactive fiction, long-form drafts, or any story world that needs room to grow.

This is not a "type one prompt and get a finished novel" tool. StoryLoom is closer to a writing desk: you set up the world and the protagonist first, then move the story forward step by step. The AI writes, polishes, and suggests. You stay in charge.

## Features

- Worldbuilding: manage the background, protagonist, characters, factions, items, and scenes.
- AI story generation: continue the story using the current world settings, summary, and recent conversation.
- Writing modes: guide generation with automatic, action, dialogue, or thinking modes.
- Prompt polishing: clean up user input before sending it to the model.
- Story suggestions: ask the AI for possible next moves based on the current context.
- Context summaries: compress older story content when the conversation gets long.
- Local saves: keep chat history and world settings on disk.
- Separate model roles: use one model for story text and another for summaries, suggestions, and polishing.

## Tech stack

- .NET 10
- C#
- WPF
- Blazor WebView
- Razor Components
- OpenAI-style Chat Completions API
- Microsoft ML Tokenizers
- Microsoft Semantic Kernel packages

The app starts as a WPF window. Most of the interface runs inside Blazor WebView, so the project keeps a desktop app shape while using Razor for the UI.

## Project layout

```text
StoryLoom/
├─ Agents/              # Agent-related code, mostly reserved for future work
├─ Data/Models/         # Data models for characters, factions, items, and scenes
├─ Display/             # WPF and Blazor UI
│  ├─ Pages/            # Saves, story generation, worldbuilding, and settings pages
│  └─ Shared/           # Layout, navigation, toast UI, and shared components
├─ Services/            # LLM calls, settings, saves, logging, and prompt templates
├─ wwwroot/             # Static files for Blazor WebView
└─ StoryLoom.csproj
```

Useful files to start with:

- `Display/Pages/WorldBuilding.razor`: worldbuilding page.
- `Display/Pages/StoryGenerator.razor`: story generation page.
- `Display/Pages/ModelSettings.razor`: model settings page.
- `Services/LlmClient.cs`: low-level model request wrapper.
- `Services/LlmService.cs`: generation, polishing, summaries, and suggestions.
- `Services/ConversationService.cs`: conversations, saves, and context summaries.
- `Services/PromptTemplates.cs`: prompt templates in one place.

## Requirements

You need:

- Windows
- .NET 10 SDK
- A model service compatible with the OpenAI Chat Completions format
- An API key for that service

The default settings are aimed at DeepSeek:

```text
Model: deepseek-chat
API URL: https://api.deepseek.com/v1
```

Other compatible APIs should work as long as they support the `/chat/completions` style endpoint.

## Run locally

From the repository root:

```powershell
dotnet restore .\StoryLoom.sln
dotnet build .\StoryLoom.sln
dotnet run --project .\StoryLoom\StoryLoom.csproj
```

You can also open `StoryLoom.sln` in Visual Studio and run the StoryLoom project from there.

## Basic workflow

1. Open the app and go to "Model settings".
2. Fill in the story model, prompt model, and global API key.
3. Test both model connections.
4. Go to "Worldbuilding" and write the background and protagonist settings.
5. Add characters, factions, items, and scenes if you need them.
6. Go to "Story generation" and enter the next plot beat, line of dialogue, or writing instruction.
7. Use suggestions, polishing, and writing modes to keep the story moving.

If the background or protagonist is missing, the story generation page sends you back to worldbuilding first.

## Saved data

The app stores config and saves under the runtime directory:

```text
Config/config.json
Saves/<save name>/chat.json
Saves/<save name>/world.json
```

- `config.json` stores model settings, temperature, context window, typewriter speed, and related app settings.
- `chat.json` stores the current story conversation and summary.
- `world.json` stores the background, protagonist, and entity data.

Do not commit local config files that contain API keys.

## Current state

StoryLoom already has the basic loop for an AI writing desktop app: configure models, build a world, generate story text, summarize long context, and save progress. The Agents folder and Semantic Kernel packages suggest a possible next step toward multi-agent writing, such as director, faction, or character agents.

## Development notes

- Most UI work lives under `Display/Pages` and `Display/Shared`.
- Shared state is mostly handled by singleton services such as `SettingsService` and `ConversationService`.
- Model requests should go through `LlmClient`; pages should not build HTTP requests directly.
- Prompt changes usually belong in `PromptTemplates`.
- Long-context behavior is handled around `ConversationService.CheckAndSummarizeAsync`.

## License

No license file is currently included. Add one before publishing the project publicly.
