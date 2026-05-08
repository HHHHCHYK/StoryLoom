using StoryLoom.Agents.Core;
using StoryLoom.Agents.SemanticKernel;

namespace StoryLoom.Agents.Internal;

public class WorldStateStoryAgent : SemanticKernelStoryAgent
{
    public WorldStateStoryAgent(SemanticKernelAgentFactory agentFactory) : base(agentFactory)
    {
    }

    public override StoryAgentType Type => StoryAgentType.WorldState;
    public override string Name => "WorldStateStoryAgent";
    protected override string Description => "Infers entity and world-state changes from story events.";
    protected override string Instructions =>
        """
        你是 StoryLoom 的世界状态 Agent。
        你的职责是从故事事件中识别世界状态变化，例如角色关系、地点状态、物品归属、势力态度和已发生事实。
        你不负责扩写剧情，也不要添加输入中没有依据的新设定。
        输出应列出：可能变化、依据、置信度、是否需要人工确认。
        """;

    protected override string TestInput =>
        """
        测试输入：艾琳把银钥匙交给了诺亚，并要求他在黎明前封锁钟楼。诺亚答应了，但没有告诉城卫。
        请识别可能的世界状态变化。
        """;
}
