using StoryLoom.Data.Models;

namespace StoryLoom.Services;

public class EntityMergeService
{
    private readonly SettingsService _settings;
    private readonly LogService _logger;

    public EntityMergeService(SettingsService settings, LogService logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public EntityChangeSet CreateChangeSet(EntityExtractionResult result, string source, EntityChangeReviewMode mode = EntityChangeReviewMode.AiResponseReview)
    {
        var changeSet = new EntityChangeSet
        {
            Mode = mode,
            Source = source,
            CreatedAt = DateTime.Now
        };

        AddChangePreviews(changeSet, result.Characters, ExtractedEntityType.Character);
        AddChangePreviews(changeSet, result.Factions, ExtractedEntityType.Faction);
        AddChangePreviews(changeSet, result.Items, ExtractedEntityType.Item);
        AddChangePreviews(changeSet, result.Scenes, ExtractedEntityType.Scene);

        return changeSet;
    }

    public bool Apply(EntityChangeSet changeSet)
    {
        var changed = false;

        changed |= MergeCharacters(changeSet.Changes.Where(change => change.Type == ExtractedEntityType.Character).Select(change => change.Entity));
        changed |= MergeFactions(changeSet.Changes.Where(change => change.Type == ExtractedEntityType.Faction).Select(change => change.Entity));
        changed |= MergeItems(changeSet.Changes.Where(change => change.Type == ExtractedEntityType.Item).Select(change => change.Entity));
        changed |= MergeScenes(changeSet.Changes.Where(change => change.Type == ExtractedEntityType.Scene).Select(change => change.Entity));

        if (changed)
        {
            _settings.NotifyStateChanged();
            _logger.Log("World entities updated from confirmed entity extraction.");
        }

        return changed;
    }

    public bool Merge(EntityExtractionResult result)
    {
        return Apply(CreateChangeSet(result, "direct"));
    }

    private void AddChangePreviews(EntityChangeSet changeSet, IEnumerable<ExtractedEntity> entities, ExtractedEntityType type)
    {
        foreach (var entity in entities.Where(entity => !string.IsNullOrWhiteSpace(entity.Name)))
        {
            var existing = FindExisting(type, entity.Name);
            if (existing == null)
            {
                changeSet.Changes.Add(new EntityChangePreview
                {
                    Type = type,
                    Kind = EntityChangeKind.Added,
                    Name = entity.Name,
                    IncomingSummary = entity.Summary,
                    IncomingDescriptions = entity.Descriptions.Where(description => !string.IsNullOrWhiteSpace(description)).Select(description => description.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    Entity = CloneEntity(entity)
                });
                continue;
            }

            var incomingDescriptions = GetNewDescriptions(existing.Descriptions, entity.Descriptions);
            var summaryWillChange = string.IsNullOrWhiteSpace(existing.Summary) && !string.IsNullOrWhiteSpace(entity.Summary);
            if (!summaryWillChange && incomingDescriptions.Count == 0)
            {
                continue;
            }

            changeSet.Changes.Add(new EntityChangePreview
            {
                Type = type,
                Kind = EntityChangeKind.Modified,
                Name = entity.Name,
                CurrentSummary = existing.Summary,
                IncomingSummary = summaryWillChange ? entity.Summary : string.Empty,
                CurrentDescriptions = existing.Descriptions.ToList(),
                IncomingDescriptions = incomingDescriptions,
                Entity = CloneEntity(entity)
            });
        }
    }

    private ExistingEntitySnapshot? FindExisting(ExtractedEntityType type, string name)
    {
        return type switch
        {
            ExtractedEntityType.Character => ToSnapshot(FindByName(_settings.Characters, name)),
            ExtractedEntityType.Faction => ToSnapshot(FindByName(_settings.Factions, name)),
            ExtractedEntityType.Item => ToSnapshot(FindByName(_settings.Items, name)),
            ExtractedEntityType.Scene => ToSnapshot(FindByName(_settings.Scenes, name)),
            _ => null
        };
    }

    private static ExistingEntitySnapshot? ToSnapshot(Character? entity)
    {
        return entity == null ? null : new ExistingEntitySnapshot(entity.Summary, entity.Descriptions.ToList());
    }

    private static ExistingEntitySnapshot? ToSnapshot(Faction? entity)
    {
        return entity == null ? null : new ExistingEntitySnapshot(entity.Summary, entity.Descriptions.ToList());
    }

    private static ExistingEntitySnapshot? ToSnapshot(Item? entity)
    {
        return entity == null ? null : new ExistingEntitySnapshot(entity.Summary, entity.Description.ToList());
    }

    private static ExistingEntitySnapshot? ToSnapshot(Scene? entity)
    {
        return entity == null ? null : new ExistingEntitySnapshot(entity.Summary, entity.Descriptions.ToList());
    }

    private static ExtractedEntity CloneEntity(ExtractedEntity entity)
    {
        return new ExtractedEntity
        {
            Name = entity.Name.Trim(),
            Summary = entity.Summary.Trim(),
            Descriptions = entity.Descriptions.Where(description => !string.IsNullOrWhiteSpace(description)).Select(description => description.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static List<string> GetNewDescriptions(IEnumerable<string> currentDescriptions, IEnumerable<string> incomingDescriptions)
    {
        return incomingDescriptions
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Select(description => description.Trim())
            .Where(description => !currentDescriptions.Any(current => string.Equals(current.Trim(), description, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool MergeCharacters(IEnumerable<ExtractedEntity> entities)
    {
        var changed = false;
        foreach (var entity in entities)
        {
            var existing = FindByName(_settings.Characters, entity.Name);
            if (existing == null)
            {
                _settings.Characters.Add(new Character
                {
                    Name = entity.Name,
                    Summary = entity.Summary,
                    Descriptions = entity.Descriptions.ToList()
                });
                changed = true;
                continue;
            }

            changed |= MergeSummaryAndDescriptions(existing, entity);
        }

        return changed;
    }

    private bool MergeFactions(IEnumerable<ExtractedEntity> entities)
    {
        var changed = false;
        foreach (var entity in entities)
        {
            var existing = FindByName(_settings.Factions, entity.Name);
            if (existing == null)
            {
                _settings.Factions.Add(new Faction
                {
                    Name = entity.Name,
                    Summary = entity.Summary,
                    Descriptions = entity.Descriptions.ToList()
                });
                changed = true;
                continue;
            }

            changed |= MergeSummaryAndDescriptions(existing, entity);
        }

        return changed;
    }

    private bool MergeItems(IEnumerable<ExtractedEntity> entities)
    {
        var changed = false;
        foreach (var entity in entities)
        {
            var existing = FindByName(_settings.Items, entity.Name);
            if (existing == null)
            {
                _settings.Items.Add(new Item
                {
                    Name = entity.Name,
                    Summary = entity.Summary,
                    Description = entity.Descriptions.ToList()
                });
                changed = true;
                continue;
            }

            changed |= MergeItem(existing, entity);
        }

        return changed;
    }

    private bool MergeScenes(IEnumerable<ExtractedEntity> entities)
    {
        var changed = false;
        foreach (var entity in entities)
        {
            var existing = FindByName(_settings.Scenes, entity.Name);
            if (existing == null)
            {
                _settings.Scenes.Add(new Scene
                {
                    Name = entity.Name,
                    Summary = entity.Summary,
                    Descriptions = entity.Descriptions.ToList()
                });
                changed = true;
                continue;
            }

            changed |= MergeSummaryAndDescriptions(existing, entity);
        }

        return changed;
    }

    private static Character? FindByName(IEnumerable<Character> entities, string name)
    {
        return entities.FirstOrDefault(entity => string.Equals(NormalizeName(entity.Name), NormalizeName(name), StringComparison.OrdinalIgnoreCase));
    }

    private static Faction? FindByName(IEnumerable<Faction> entities, string name)
    {
        return entities.FirstOrDefault(entity => string.Equals(NormalizeName(entity.Name), NormalizeName(name), StringComparison.OrdinalIgnoreCase));
    }

    private static Scene? FindByName(IEnumerable<Scene> entities, string name)
    {
        return entities.FirstOrDefault(entity => string.Equals(NormalizeName(entity.Name), NormalizeName(name), StringComparison.OrdinalIgnoreCase));
    }

    private static Item? FindByName(IEnumerable<Item> entities, string name)
    {
        return entities.FirstOrDefault(entity => string.Equals(NormalizeName(entity.Name), NormalizeName(name), StringComparison.OrdinalIgnoreCase));
    }

    private static bool MergeSummaryAndDescriptions(Character existing, ExtractedEntity entity)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(existing.Summary) && !string.IsNullOrWhiteSpace(entity.Summary))
        {
            existing.Summary = entity.Summary;
            changed = true;
        }

        changed |= MergeDescriptions(existing.Descriptions, entity.Descriptions);
        return changed;
    }

    private static bool MergeSummaryAndDescriptions(Faction existing, ExtractedEntity entity)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(existing.Summary) && !string.IsNullOrWhiteSpace(entity.Summary))
        {
            existing.Summary = entity.Summary;
            changed = true;
        }

        changed |= MergeDescriptions(existing.Descriptions, entity.Descriptions);
        return changed;
    }

    private static bool MergeSummaryAndDescriptions(Scene existing, ExtractedEntity entity)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(existing.Summary) && !string.IsNullOrWhiteSpace(entity.Summary))
        {
            existing.Summary = entity.Summary;
            changed = true;
        }

        changed |= MergeDescriptions(existing.Descriptions, entity.Descriptions);
        return changed;
    }

    private static bool MergeItem(Item existing, ExtractedEntity entity)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(existing.Summary) && !string.IsNullOrWhiteSpace(entity.Summary))
        {
            existing.Summary = entity.Summary;
            changed = true;
        }

        changed |= MergeDescriptions(existing.Description, entity.Descriptions);
        return changed;
    }

    private static bool MergeDescriptions(ICollection<string> target, IEnumerable<string> descriptions)
    {
        var changed = false;
        foreach (var description in descriptions.Where(description => !string.IsNullOrWhiteSpace(description)))
        {
            if (target.Any(existing => string.Equals(existing.Trim(), description.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            target.Add(description.Trim());
            changed = true;
        }

        return changed;
    }

    private static string NormalizeName(string value)
    {
        return string.Concat((value ?? string.Empty).Where(character => !char.IsWhiteSpace(character))).Trim();
    }

    private record ExistingEntitySnapshot(string Summary, List<string> Descriptions);
}
