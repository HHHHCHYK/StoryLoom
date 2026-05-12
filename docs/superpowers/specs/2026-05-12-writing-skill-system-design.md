# StoryLoom Writing Skill System Design

## Goal

Build an in-app Writing Skill system for StoryLoom. A Writing Skill is a reusable writing capability package for a single story save. It guides story generation with user-defined style, rules, process, forbidden patterns, examples, and notes.

The first version supports one active skill at a time, multiple skills per save, manual editing, AI-assisted evolution, and version history.

## Confirmed Scope

- Store skills per story save, not globally.
- Use a dedicated `writing-skills.json` file in each save directory.
- Support multiple Writing Skills, with one active skill used during generation.
- Use a full structured skill shape:
  - Name
  - Purpose
  - Style description
  - Writing rules
  - Writing process
  - Forbidden patterns
  - Example text
  - Notes
- Save manual edits as new versions.
- Let AI generate a complete evolved skill snapshot from the current skill and a user-provided evolution goal.
- Save AI evolution only after user confirmation.
- Preserve version history and parent version relationships.
- Support rollback by creating a new rollback version from an older snapshot.

Out of scope for the first version:

- Global skill library.
- Cross-save skill sharing.
- Multiple active skills at once.
- Automatic skill selection by action type.
- Batch deletion.
- Automatic evolution without user confirmation.
- Save list metadata based on skills.

## Data Model

Add `StoryLoom/Data/Models/WritingSkill.cs`.

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

Design notes:

- `WritingSkillProfile` represents the identity of a skill.
- `WritingSkillProfile.Name` is retained as a display/cache field and must always mirror the current version's `Snapshot.Name` whenever a new version is created.
- `WritingSkillVersion` represents a historical version of that skill.
- The active content is the `Snapshot` in the current version.
- AI evolution and rollback never overwrite history.
- `WritingSkillProfile.UpdatedAt` changes only when a new version is created. Switching the active skill does not update it.

## Storage

Each save directory gets one skill file:

```text
Saves/<save name>/writing-skills.json
```

Old saves without this file load normally. When a save has no skill file, the app creates an empty `WritingSkillLibrary` in memory and saves it when the user changes skill data.

## Service Design

Add `StoryLoom/Services/WritingSkillService.cs`.

Responsibilities:

- Track the current save name.
- Load and save `writing-skills.json`.
- Expose the current `WritingSkillLibrary`.
- Expose the active skill profile and active skill snapshot.
- Create a new skill.
- Delete one explicit skill.
- Set the active skill.
- Save manual edits as a new version.
- Roll back by creating a new rollback version from an older snapshot.
- Generate an AI evolution candidate.
- Confirm an AI evolution candidate as a new version.
- Raise `OnChanged` after changes.

Lifetime and event consumers:

- Register `WritingSkillService` as a singleton in `MainWindow.xaml.cs`.
- `WritingSkills.razor` subscribes to `OnChanged` to refresh the skill list, editor, version list, and candidate preview.
- `StoryGenerator.razor` subscribes to `OnChanged` to refresh the compact active skill selector.
- Both Razor components must unsubscribe during disposal.

Collaboration with `ConversationService`:

- `StartNewConversationAsync()` calls `WritingSkillService.StartNewLibraryAsync(saveName)` after creating the save directory.
- `LoadSaveAsync(saveName)` calls `WritingSkillService.LoadLibraryAsync(saveName)` after loading chat and world data.
- `SaveCurrentStateAsync()` does not save skill data.
- `GetAvailableSavesAsync()` does not read skill data in the first version.

Default behavior:

- No active skill means generation behaves exactly as it does today.
- Creating the first skill automatically makes it active.
- Deleting the active skill selects the first remaining skill. If none remain, active skill is cleared.
- Loading a library, starting a new library, deleting a skill, or generating a new AI candidate clears stale AI evolution candidate state when appropriate so candidate previews never leak across saves or skills.

## Prompt Integration

Extend the generation path:

```text
StoryGenerator.razor
-> WritingSkillService.ActiveSkillSnapshot
-> LlmService.StartGenerateAsync(..., activeWritingSkill)
-> PromptTemplates.StoryGenerationSystemPrompt(..., activeWritingSkill)
```

If no active skill exists, do not inject skill content.

If an active skill exists, inject a `WritingSkill` section into the system prompt:

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

Every skill field must be XML-escaped before injection. User text can contain strings such as `</WritingSkill>`, quotes, ampersands, or Windows paths; these must be preserved as text and must not break the prompt structure.

The method signature remains backward compatible by adding a nullable default parameter:

```csharp
public IAsyncEnumerable<string> StartGenerateAsync(
    List<ChatMessage> historyMessages,
    string background,
    string protagonist,
    string summary,
    string? actionType = null,
    WritingSkillSnapshot? activeWritingSkill = null)
```

`<WritingSkill>` is inserted after `<Protagonist>` and before `<CurrentStoryState_Summary>`:

```text
directives
<WorldBackground>
<Protagonist>
<WritingSkill>
<CurrentStoryState_Summary>
final instruction
```

Prompt priority:

- World background, protagonist, and story continuity remain authoritative for facts.
- Writing rules, forbidden patterns, and writing process guide how the text is written.
- Style description and example text guide prose texture and voice.
- The selected action type still constrains the immediate generated passage.
- The model must still output pure story prose only.

## AI Evolution

Add a prompt template such as:

```csharp
public static string EvolveWritingSkill(WritingSkillSnapshot current, string evolutionGoal)
```

The prompt asks the model to produce a complete `WritingSkillSnapshot` JSON object. It must include every field, even if a field stays unchanged.

Flow:

1. User selects a skill.
2. User enters an evolution goal.
3. `WritingSkillService` asks the prompt model for a candidate snapshot.
4. Service parses the JSON into `WritingSkillSnapshot`.
5. UI displays the candidate.
6. User confirms or cancels.
7. Confirm creates an `AiEvolution` version with `ParentVersionId` set to the previous current version.
8. Cancel changes nothing.

Model and validation boundaries:

- Use the configured prompt model for evolution.
- Use an output budget large enough for complete JSON. The first implementation uses `maxTokens` of 4096.
- The service validates that the response is parseable as `WritingSkillSnapshot`.
- Content quality is reviewed by the user in the preview. The first version does not attempt semantic validation for contradictory rules, weak examples, or low-quality prose guidance.
- If parsing fails or the candidate is empty, the current skill remains unchanged.

## UI Design

Add `StoryLoom/Display/Pages/WritingSkills.razor`.

Navigation:

- Add a "Writing Skill" entry to the app navigation as item `05`, after the existing `01` through `04` entries. Do not renumber existing navigation items.

Layout:

- Left column: skill list.
- Middle column: skill editor.
- Right column: version history and AI evolution controls.

Skill list actions:

- New skill.
- Select skill.
- Set active skill.
- Delete the selected explicit skill.

Editor fields:

- Name.
- Purpose.
- Style description.
- Writing rules.
- Writing process.
- Forbidden patterns.
- Example text.
- Notes.
- Version note.

Editor actions:

- Save as new version.
- Reset unsaved changes.

Version actions:

- View snapshot.
- Roll back to this version.

AI evolution actions:

- Enter evolution goal.
- Generate candidate.
- Preview full candidate fields.
- Confirm candidate as new version.
- Cancel candidate.

Story generation page:

- Add a compact skill selector in the toolbar.
- Include "No Skill" as an option.
- Include a link or button to manage Writing Skills.

## Error Handling

- Invalid or unreadable `writing-skills.json`: log the error and load an empty library so the save remains usable.
- Failed writes to `writing-skills.json`: catch the exception, log it, and keep the app usable. Do not throw from UI-triggered save paths.
- AI evolution JSON parse failure: show a toast and do not change the current skill.
- Missing selected version: log and keep the current version unchanged.
- Empty skill library: generation continues without skill injection.
- Save switching: clear unconfirmed AI evolution candidates before loading the next save's library.

## Testing Plan

Unit-level or service-focused checks:

- New library starts empty.
- First skill becomes active.
- Manual edit creates a new version.
- AI evolution confirmation creates a new version with the correct parent.
- Rollback creates a new rollback version without deleting history.
- Deleting the active skill updates active selection safely.
- Loading an old save without `writing-skills.json` does not fail.
- Loading a different save clears any unconfirmed AI evolution candidate.
- Saving and reloading snapshot text preserves quotes, backslashes, ampersands, and XML-like strings such as `</WritingSkill>`.
- Prompt injection escapes XML-sensitive characters before the skill enters the system prompt.

Integration checks:

- Build succeeds.
- Existing generation still works with no active skill.
- Generation prompt includes skill content when a skill is active.
- Skill data persists across save reload.
- Switching saves isolates active skill and candidate state per save.
- Write failures are logged without crashing the UI path.

Manual UI checks:

- Create skill.
- Switch active skill.
- Edit and save skill.
- Generate AI evolution candidate.
- Confirm and cancel candidate.
- View and roll back versions.

## Implementation Phases

Phase 1: Model and persistence

- Add `WritingSkill` model classes.
- Add `WritingSkillService`.
- Register `WritingSkillService` as a singleton in dependency injection.
- Hook save load and save creation from `ConversationService`.

Phase 2: Prompt integration

- Extend `LlmService.StartGenerateAsync` with `WritingSkillSnapshot? activeWritingSkill = null`.
- Extend `PromptTemplates.StoryGenerationSystemPrompt`.
- Add `PromptTemplates.EvolveWritingSkill`.
- Inject `WritingSkillService` into `StoryGenerator.razor`.
- Read `WritingSkillService.ActiveSkillSnapshot` during generation.
- Pass the active snapshot into `LlmService.StartGenerateAsync`.

Phase 3: Management UI

- Add `WritingSkills.razor`.
- Add navigation entry.
- Implement skill list, editor, version history, rollback, and AI evolution preview.

Phase 4: Story page selector

- Add compact active skill selector to `StoryGenerator.razor`.
- Include "No Skill" and manage-page navigation.

Phase 5: Verification and polish

- Run build.
- Test save/load behavior.
- Test no-skill compatibility.
- Test active-skill generation.
- Adjust UI labels and layout after first use.
