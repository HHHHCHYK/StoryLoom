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
