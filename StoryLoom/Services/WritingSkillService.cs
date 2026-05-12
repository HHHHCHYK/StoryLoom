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

    public Task StartNewLibraryAsync(string saveName)
    {
        CurrentSaveName = saveName;
        Library = new WritingSkillLibrary();
        ClearEvolutionCandidate();

        NotifyChanged();
        return Task.CompletedTask;
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
                _logger.Log($"Writing skill library not found: {CurrentFilePath}", LogLevel.Warning);
                NotifyChanged();
                return;
            }

            var json = await File.ReadAllTextAsync(CurrentFilePath);
            Library = JsonSerializer.Deserialize<WritingSkillLibrary>(json, _jsonOptions) ?? new WritingSkillLibrary();
            NormalizeLibrary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load writing skill library");
            Library = new WritingSkillLibrary();
        }

        NotifyChanged();
    }

    public async Task SaveLibraryAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentSaveName))
        {
            _logger.Log("Cannot save writing skill library without a current save name.", LogLevel.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(CurrentSaveDirectory);

            var json = JsonSerializer.Serialize(Library, _jsonOptions);
            await File.WriteAllTextAsync(CurrentFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save writing skill library");
        }
    }

    public void ClearEvolutionCandidate()
    {
        EvolutionCandidate = null;
        EvolutionCandidateParentVersionId = string.Empty;
    }

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

    public async Task<WritingSkillProfile> CreateSkillAsync(WritingSkillSnapshot snapshot, string userNote = "")
    {
        var sanitizedSnapshot = SanitizeSnapshot(snapshot);
        var version = new WritingSkillVersion
        {
            VersionNumber = 1,
            Source = WritingSkillVersionSource.Initial,
            Snapshot = sanitizedSnapshot,
            UserNote = userNote ?? string.Empty
        };

        var profile = new WritingSkillProfile
        {
            Name = sanitizedSnapshot.Name,
            CurrentVersionId = version.Id,
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
        else
        {
            _logger.Log($"Cannot activate missing writing skill: {skillId}", LogLevel.Warning);
            return;
        }

        await SaveLibraryAsync();
        NotifyChanged();
    }

    public async Task DeleteSkillAsync(string skillId)
    {
        var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
        if (skill is null)
        {
            _logger.Log($"Cannot delete missing writing skill: {skillId}", LogLevel.Warning);
            return;
        }

        Library.Skills.Remove(skill);
        ClearEvolutionCandidate();

        if (Library.ActiveSkillId == skillId)
        {
            Library.ActiveSkillId = Library.Skills.FirstOrDefault()?.Id ?? string.Empty;
        }

        await SaveLibraryAsync();
        NotifyChanged();
    }

    public Task<WritingSkillVersion?> SaveManualVersionAsync(
        string skillId,
        WritingSkillSnapshot snapshot,
        string userNote = "") =>
        AddVersionAsync(skillId, snapshot, WritingSkillVersionSource.ManualEdit, userNote);

    public async Task<WritingSkillVersion?> RollbackToVersionAsync(
        string skillId,
        string versionId,
        string userNote = "")
    {
        var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
        var oldVersion = skill?.Versions.FirstOrDefault(version => version.Id == versionId);
        if (skill is null || oldVersion is null)
        {
            _logger.Log($"Cannot rollback missing writing skill version: {skillId}/{versionId}", LogLevel.Warning);
            return null;
        }

        return await AddVersionAsync(
            skillId,
            oldVersion.Snapshot.Clone(),
            WritingSkillVersionSource.Rollback,
            userNote,
            oldVersion.Id);
    }

    public async Task<WritingSkillVersion?> ConfirmEvolutionCandidateAsync(string skillId, string userNote = "")
    {
        if (EvolutionCandidate is null)
        {
            return null;
        }

        var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
        if (skill is null ||
            !skill.Versions.Any(version => version.Id == EvolutionCandidateParentVersionId))
        {
            _logger.Log(
                $"Cannot confirm writing skill evolution candidate for mismatched skill. SkillId: {skillId}, ParentVersionId: {EvolutionCandidateParentVersionId}",
                LogLevel.Warning);
            ClearEvolutionCandidate();
            NotifyChanged();
            return null;
        }

        var version = await AddVersionAsync(
            skillId,
            EvolutionCandidate.Clone(),
            WritingSkillVersionSource.AiEvolution,
            userNote,
            EvolutionCandidateParentVersionId);

        ClearEvolutionCandidate();
        NotifyChanged();
        return version;
    }

    public async Task<WritingSkillVersion?> AddVersionAsync(
        string skillId,
        WritingSkillSnapshot snapshot,
        WritingSkillVersionSource source,
        string userNote = "",
        string parentVersionId = "")
    {
        var skill = Library.Skills.FirstOrDefault(item => item.Id == skillId);
        if (skill is null)
        {
            _logger.Log($"Cannot add version to missing writing skill: {skillId}", LogLevel.Warning);
            return null;
        }

        skill.Versions ??= [];

        var sanitizedSnapshot = SanitizeSnapshot(snapshot);
        var currentVersionId = skill.CurrentVersionId;
        var version = new WritingSkillVersion
        {
            VersionNumber = skill.Versions.Count == 0 ? 1 : skill.Versions.Max(item => item.VersionNumber) + 1,
            Source = source,
            ParentVersionId = string.IsNullOrWhiteSpace(parentVersionId) ? currentVersionId : parentVersionId,
            Snapshot = sanitizedSnapshot,
            UserNote = userNote ?? string.Empty
        };

        skill.Versions.Add(version);
        skill.CurrentVersionId = version.Id;
        skill.Name = sanitizedSnapshot.Name;
        skill.UpdatedAt = DateTime.Now;

        await SaveLibraryAsync();
        NotifyChanged();
        return version;
    }

    private void NormalizeLibrary()
    {
        Library ??= new WritingSkillLibrary();
        Library.Skills ??= [];

        foreach (var skill in Library.Skills)
        {
            skill.Versions ??= [];

            if (!skill.Versions.Any())
            {
                skill.CurrentVersionId = string.Empty;
                skill.Name ??= string.Empty;
                continue;
            }

            if (string.IsNullOrWhiteSpace(skill.CurrentVersionId)
                || !skill.Versions.Any(version => version.Id == skill.CurrentVersionId))
            {
                skill.CurrentVersionId = skill.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .ThenByDescending(version => version.CreatedAt)
                    .First()
                    .Id;
            }

            var currentSnapshot = skill.CurrentSnapshot;
            skill.Name = currentSnapshot?.Name ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Library.ActiveSkillId)
            || !Library.Skills.Any(skill => skill.Id == Library.ActiveSkillId))
        {
            Library.ActiveSkillId = Library.Skills.FirstOrDefault()?.Id ?? string.Empty;
        }
    }

    private WritingSkillSnapshot SanitizeSnapshot(WritingSkillSnapshot snapshot)
    {
        var sanitized = (snapshot ?? new WritingSkillSnapshot()).Clone();

        sanitized.Name = string.IsNullOrWhiteSpace(sanitized.Name) ? "未命名 Skill" : sanitized.Name.Trim();
        sanitized.Purpose ??= string.Empty;
        sanitized.StyleDescription ??= string.Empty;
        sanitized.WritingRules ??= string.Empty;
        sanitized.WritingProcess ??= string.Empty;
        sanitized.ForbiddenPatterns ??= string.Empty;
        sanitized.ExampleText ??= string.Empty;
        sanitized.Notes ??= string.Empty;

        return sanitized;
    }

    private void NotifyChanged() => OnChanged?.Invoke();
}
