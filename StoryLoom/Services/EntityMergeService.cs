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

    public bool Merge(EntityExtractionResult result)
    {
        var changed = false;

        changed |= MergeCharacters(result.Characters);
        changed |= MergeFactions(result.Factions);
        changed |= MergeItems(result.Items);
        changed |= MergeScenes(result.Scenes);

        if (changed)
        {
            _settings.NotifyStateChanged();
            _logger.Log("World entities updated from entity extraction.");
        }

        return changed;
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
}
