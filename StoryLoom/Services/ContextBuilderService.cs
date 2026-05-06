using System.Text;
using StoryLoom.Data.Models;

namespace StoryLoom.Services;

public class ContextBuilderService
{
    public GenerationContext Build(StoryProject project, ContextBuildRequest request)
    {
        var activeDocument = SelectDocument(project, request.DocumentId);
        var recentNodes = SelectRecentNodes(activeDocument, request.RecentNodeCount);
        var entityIds = CollectEntityIds(request, recentNodes);
        var relevantMemories = SelectRelevantMemories(project, entityIds, request.MemoryCount);
        var relevantTimelineEvents = SelectRelevantTimelineEvents(project, entityIds, request.TimelineEventCount);
        var activeDrive = BuildActiveDrive(project.Drive, entityIds, request);

        return new GenerationContext
        {
            ProjectId = project.Id,
            DocumentId = activeDocument?.Id ?? request.DocumentId,
            WorldSnapshot = BuildWorldSnapshot(project.World, request, entityIds),
            ActiveDrive = activeDrive,
            RecentNodes = recentNodes,
            RelevantMemories = relevantMemories,
            RelevantTimelineEvents = relevantTimelineEvents,
            UserInstruction = request.UserInstruction,
            Constraints = activeDrive.Constraints,
            Mode = request.Mode,
            Depth = request.Depth
        };
    }

    public string BuildPromptContext(GenerationContext context)
    {
        var builder = new StringBuilder();

        AppendSection(builder, "故事世界", BuildWorldSection(context.WorldSnapshot));
        AppendSection(builder, "当前驱动力", BuildDriveSection(context.ActiveDrive, context.Depth));
        AppendSection(builder, "近期正文", BuildRecentNodesSection(context.RecentNodes));
        AppendSection(builder, "相关记忆", BuildMemoriesSection(context.RelevantMemories));
        AppendSection(builder, "相关时间线", BuildTimelineSection(context.RelevantTimelineEvents));
        AppendSection(builder, "约束", BuildConstraintsSection(context.Constraints));
        AppendSection(builder, "用户输入", context.UserInstruction);

        return builder.ToString().Trim();
    }

    private static StoryDocument? SelectDocument(StoryProject project, string documentId)
    {
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            var matchedDocument = project.Documents.FirstOrDefault(document => document.Id == documentId);
            if (matchedDocument != null)
            {
                return matchedDocument;
            }
        }

        return project.Documents
            .OrderBy(document => document.Position)
            .FirstOrDefault(document => document.Type == StoryDocumentType.MainStory)
            ?? project.Documents.OrderBy(document => document.Position).FirstOrDefault();
    }

    private static List<StoryNode> SelectRecentNodes(StoryDocument? document, int count)
    {
        if (document == null || count <= 0)
        {
            return [];
        }

        return document.Nodes
            .OrderByDescending(node => node.Position)
            .Take(count)
            .OrderBy(node => node.Position)
            .ToList();
    }

    private static HashSet<string> CollectEntityIds(ContextBuildRequest request, IEnumerable<StoryNode> recentNodes)
    {
        var entityIds = new HashSet<string>(request.FocusEntityIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        foreach (var node in recentNodes)
        {
            foreach (var entityRef in node.EntityRefs.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                entityIds.Add(entityRef);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentSceneId))
        {
            entityIds.Add(request.CurrentSceneId);
        }

        return entityIds;
    }

    private static List<StoryMemory> SelectRelevantMemories(StoryProject project, HashSet<string> entityIds, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        return project.Memories
            .Where(memory => memory.Status == StoryMemoryStatus.Active)
            .Select(memory => new
            {
                Memory = memory,
                Score = GetEntityOverlapScore(memory.EntityRefs, entityIds) + memory.Importance
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Memory.UpdatedAt)
            .Take(count)
            .Select(item => item.Memory)
            .ToList();
    }

    private static List<TimelineEvent> SelectRelevantTimelineEvents(StoryProject project, HashSet<string> entityIds, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        return project.Timeline
            .Select(timelineEvent => new
            {
                TimelineEvent = timelineEvent,
                Score = GetEntityOverlapScore(timelineEvent.EntityRefs, entityIds) + timelineEvent.Importance
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.TimelineEvent.Order)
            .Take(count)
            .Select(item => item.TimelineEvent)
            .ToList();
    }

    private static NarrativeDrive BuildActiveDrive(NarrativeDrive sourceDrive, HashSet<string> entityIds, ContextBuildRequest request)
    {
        var actorDrives = sourceDrive.ActorDrives
            .Select(actorDrive => new
            {
                ActorDrive = actorDrive,
                Score = GetActorDriveScore(actorDrive, entityIds)
            })
            .Where(item => item.Score > 0 || request.Mode == GenerationMode.Deep)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.ActorDrive.Influence)
            .Take(GetActorDriveLimit(request.Mode))
            .Select(item => item.ActorDrive)
            .ToList();

        var actorDriveIds = actorDrives.Select(actorDrive => actorDrive.Id).ToHashSet();
        var goalIds = actorDrives
            .SelectMany(actorDrive => actorDrive.Goals)
            .Where(goal => goal.Status is StoryGoalStatus.Active or StoryGoalStatus.Hidden)
            .Select(goal => goal.Id)
            .ToHashSet();

        return new NarrativeDrive
        {
            ActorDrives = actorDrives,
            Relations = sourceDrive.Relations
                .Where(relation => actorDriveIds.Contains(relation.SourceActorDriveId) || actorDriveIds.Contains(relation.TargetActorDriveId))
                .Take(GetRelationLimit(request.Mode))
                .ToList(),
            Opportunities = sourceDrive.Opportunities
                .Where(opportunity => IsRelevantOpportunity(opportunity, entityIds, goalIds))
                .OrderByDescending(opportunity => opportunity.Urgency)
                .Take(GetElementLimit(request.Mode))
                .ToList(),
            Conflicts = sourceDrive.Conflicts
                .Where(conflict => conflict.Status is NarrativeConflictStatus.Active or NarrativeConflictStatus.Escalated)
                .Where(conflict => IsRelevantConflict(conflict, entityIds, goalIds))
                .OrderByDescending(conflict => conflict.Intensity)
                .Take(GetElementLimit(request.Mode))
                .ToList(),
            Constraints = sourceDrive.Constraints
                .Where(constraint => constraint.IsActive)
                .Where(constraint => IsRelevantConstraint(constraint, entityIds, goalIds))
                .OrderByDescending(constraint => constraint.Strength)
                .Take(GetElementLimit(request.Mode))
                .ToList(),
            Clues = sourceDrive.Clues
                .Where(clue => IsRelevantClue(clue, goalIds))
                .OrderByDescending(clue => clue.Reliability)
                .Take(GetElementLimit(request.Mode))
                .ToList(),
            Stakes = sourceDrive.Stakes
                .Where(stake => stake.Status is NarrativeStakeStatus.Active or NarrativeStakeStatus.Increased)
                .Where(stake => string.IsNullOrWhiteSpace(stake.RelatedGoalId) || goalIds.Contains(stake.RelatedGoalId))
                .OrderByDescending(stake => stake.Severity)
                .Take(GetElementLimit(request.Mode))
                .ToList(),
            ProgressMarkers = sourceDrive.ProgressMarkers
                .Where(marker => string.IsNullOrWhiteSpace(marker.RelatedGoalId) || goalIds.Contains(marker.RelatedGoalId))
                .OrderBy(marker => marker.Order)
                .Take(GetElementLimit(request.Mode))
                .ToList()
        };
    }

    private static StoryWorldSnapshot BuildWorldSnapshot(StoryWorld world, ContextBuildRequest request, HashSet<string> entityIds)
    {
        var currentScene = world.Scenes.FirstOrDefault(scene => scene.Name == request.CurrentSceneId || scene.Summary == request.CurrentSceneId);
        var relevantEntities = new List<StoryEntityBrief>();

        relevantEntities.AddRange(world.Characters
            .Where(character => IsEntityRelevant(character.Name, entityIds))
            .Select(character => new StoryEntityBrief
            {
                OneLine = character.Name,
                ShortSummary = character.Summary,
                KeyFacts = character.Descriptions.Take(3).ToList(),
                ActiveTags = ["Character"]
            }));

        relevantEntities.AddRange(world.Factions
            .Where(faction => IsEntityRelevant(faction.Name, entityIds))
            .Select(faction => new StoryEntityBrief
            {
                OneLine = faction.Name,
                ShortSummary = faction.Summary,
                KeyFacts = faction.Descriptions.Take(3).ToList(),
                ActiveTags = ["Faction"]
            }));

        relevantEntities.AddRange(world.Items
            .Where(item => IsEntityRelevant(item.Name, entityIds))
            .Select(item => new StoryEntityBrief
            {
                OneLine = item.Name,
                ShortSummary = item.Summary,
                KeyFacts = item.Description.Take(3).ToList(),
                ActiveTags = ["Item"]
            }));

        relevantEntities.AddRange(world.Scenes
            .Where(scene => IsEntityRelevant(scene.Name, entityIds))
            .Select(scene => new StoryEntityBrief
            {
                OneLine = scene.Name,
                ShortSummary = scene.Summary,
                KeyFacts = scene.Descriptions.Take(3).ToList(),
                ActiveTags = ["Scene"]
            }));

        return new StoryWorldSnapshot
        {
            Background = world.Background,
            Protagonist = world.Protagonist,
            CurrentSceneId = request.CurrentSceneId,
            CurrentSceneSummary = currentScene?.Summary ?? string.Empty,
            RelevantEntities = relevantEntities
                .DistinctBy(entity => entity.OneLine)
                .Take(GetEntityLimit(request.Mode))
                .ToList(),
            ActiveWorldRules = world.Rules.Take(GetWorldRuleLimit(request.Mode)).ToList()
        };
    }

    private static string BuildWorldSection(StoryWorldSnapshot snapshot)
    {
        var lines = new List<string>();
        AddLine(lines, "背景", snapshot.Background);
        AddLine(lines, "主角", snapshot.Protagonist);
        AddLine(lines, "当前场景", snapshot.CurrentSceneSummary);
        lines.AddRange(snapshot.RelevantEntities.Select(entity => $"- {entity.OneLine}: {entity.ShortSummary}"));
        lines.AddRange(snapshot.ActiveWorldRules.Select(rule => $"- 世界规则: {rule}"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildDriveSection(NarrativeDrive drive, ContextDepth depth)
    {
        var lines = new List<string>();

        foreach (var actorDrive in drive.ActorDrives)
        {
            lines.Add($"- {actorDrive.DisplayName}: {actorDrive.Brief.ShortSummary}");

            foreach (var goal in actorDrive.Goals.Where(goal => goal.Status is StoryGoalStatus.Active or StoryGoalStatus.Hidden).Take(2))
            {
                lines.Add($"  目标: {goal.Title} / {goal.DesiredOutcome}");
            }

            if (depth != ContextDepth.Brief)
            {
                foreach (var pressure in actorDrive.Pressures.OrderByDescending(pressure => pressure.Intensity).Take(2))
                {
                    lines.Add($"  压力: {pressure.Description}");
                }
            }
        }

        foreach (var conflict in drive.Conflicts)
        {
            lines.Add($"- 冲突: {conflict.Title} / {conflict.Description}");
        }

        foreach (var opportunity in drive.Opportunities)
        {
            lines.Add($"- 机会: {opportunity.Title} / {opportunity.Description}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRecentNodesSection(IEnumerable<StoryNode> nodes)
    {
        return string.Join(Environment.NewLine, nodes.Select(node => $"- {node.Role}: {node.SummaryOrText()}"));
    }

    private static string BuildMemoriesSection(IEnumerable<StoryMemory> memories)
    {
        return string.Join(Environment.NewLine, memories.Select(memory => $"- {memory.Type}: {memory.Content}"));
    }

    private static string BuildTimelineSection(IEnumerable<TimelineEvent> timelineEvents)
    {
        return string.Join(Environment.NewLine, timelineEvents.Select(timelineEvent => $"- {timelineEvent.Title}: {timelineEvent.Summary}"));
    }

    private static string BuildConstraintsSection(IEnumerable<NarrativeConstraint> constraints)
    {
        return string.Join(Environment.NewLine, constraints.Select(constraint => $"- {constraint.Type}: {constraint.Description}"));
    }

    private static int GetEntityOverlapScore(IEnumerable<string> refs, HashSet<string> entityIds)
    {
        return refs.Count(entityIds.Contains);
    }

    private static double GetActorDriveScore(NarrativeActorDrive actorDrive, HashSet<string> entityIds)
    {
        var score = actorDrive.Influence;

        if (entityIds.Contains(actorDrive.ActorId) || entityIds.Contains(actorDrive.DisplayName))
        {
            score += 10;
        }

        score += actorDrive.Goals.Sum(goal => GetEntityOverlapScore(goal.RelatedEntityIds, entityIds));
        score += actorDrive.Goals.Sum(goal => GetEntityOverlapScore(goal.RelatedMemoryIds, entityIds) * 0.5);
        score += actorDrive.State.ActiveFlags.Count(entityIds.Contains);

        return score;
    }

    private static bool IsRelevantOpportunity(NarrativeOpportunity opportunity, HashSet<string> entityIds, HashSet<string> goalIds)
    {
        return opportunity.Status == NarrativeOpportunityStatus.Available
            && (string.IsNullOrWhiteSpace(opportunity.RelatedGoalId) || goalIds.Contains(opportunity.RelatedGoalId))
            && (string.IsNullOrWhiteSpace(opportunity.SourceEntityId) || entityIds.Count == 0 || entityIds.Contains(opportunity.SourceEntityId) || opportunity.RelatedEntityIds.Any(entityIds.Contains));
    }

    private static bool IsRelevantConflict(NarrativeConflict conflict, HashSet<string> entityIds, HashSet<string> goalIds)
    {
        return string.IsNullOrWhiteSpace(conflict.RelatedGoalId)
            || goalIds.Contains(conflict.RelatedGoalId)
            || conflict.InvolvedEntityIds.Any(entityIds.Contains);
    }

    private static bool IsRelevantConstraint(NarrativeConstraint constraint, HashSet<string> entityIds, HashSet<string> goalIds)
    {
        return constraint.RelatedGoalIds.Count == 0
            || constraint.RelatedGoalIds.Any(goalIds.Contains)
            || constraint.RelatedEntityIds.Any(entityIds.Contains);
    }

    private static bool IsRelevantClue(NarrativeClue clue, HashSet<string> goalIds)
    {
        return clue.Status is NarrativeClueStatus.Discovered or NarrativeClueStatus.Verified or NarrativeClueStatus.Misleading
            && (string.IsNullOrWhiteSpace(clue.RelatedGoalId) || goalIds.Contains(clue.RelatedGoalId));
    }

    private static bool IsEntityRelevant(string idOrName, HashSet<string> entityIds)
    {
        return entityIds.Count == 0 || entityIds.Contains(idOrName);
    }

    private static int GetActorDriveLimit(GenerationMode mode)
    {
        return mode switch
        {
            GenerationMode.Deep => 8,
            GenerationMode.Standard => 5,
            _ => 3
        };
    }

    private static int GetRelationLimit(GenerationMode mode)
    {
        return mode switch
        {
            GenerationMode.Deep => 12,
            GenerationMode.Standard => 6,
            _ => 2
        };
    }

    private static int GetElementLimit(GenerationMode mode)
    {
        return mode switch
        {
            GenerationMode.Deep => 8,
            GenerationMode.Standard => 4,
            _ => 2
        };
    }

    private static int GetEntityLimit(GenerationMode mode)
    {
        return mode switch
        {
            GenerationMode.Deep => 12,
            GenerationMode.Standard => 8,
            _ => 5
        };
    }

    private static int GetWorldRuleLimit(GenerationMode mode)
    {
        return mode switch
        {
            GenerationMode.Deep => 8,
            GenerationMode.Standard => 5,
            _ => 3
        };
    }

    private static void AppendSection(StringBuilder builder, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        builder.AppendLine($"## {title}");
        builder.AppendLine(content.Trim());
        builder.AppendLine();
    }

    private static void AddLine(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value}");
        }
    }
}

[Serializable]
public class ContextBuildRequest
{
    public string DocumentId { get; set; } = string.Empty;
    public string CurrentSceneId { get; set; } = string.Empty;
    public List<string> FocusEntityIds { get; set; } = [];
    public string UserInstruction { get; set; } = string.Empty;
    public GenerationMode Mode { get; set; } = GenerationMode.Lightweight;
    public ContextDepth Depth { get; set; } = ContextDepth.Brief;
    public int RecentNodeCount { get; set; } = 8;
    public int MemoryCount { get; set; } = 6;
    public int TimelineEventCount { get; set; } = 5;
}

public static class StoryNodeExtensions
{
    public static string SummaryOrText(this StoryNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Summary))
        {
            return node.Summary;
        }

        if (node.Text.Length <= 240)
        {
            return node.Text;
        }

        return node.Text[..240] + "...";
    }
}
