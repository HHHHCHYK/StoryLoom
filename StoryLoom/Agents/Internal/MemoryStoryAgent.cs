using StoryLoom.Agents.Core;
using StoryLoom.Agents.SemanticKernel;

namespace StoryLoom.Agents.Internal;

public class MemoryStoryAgent : SemanticKernelStoryAgent
{
    public MemoryStoryAgent(SemanticKernelAgentFactory agentFactory) : base(agentFactory)
    {
    }

    public override StoryAgentType Type => StoryAgentType.Memory;
    public override string Name => "MemoryStoryAgent";
    protected override string Description => "Extracts long-term memories, facts, promises, and relationship changes.";
    protected override string Instructions =>
        """
        你是 StoryLoom 的记忆 Agent。
        你的职责是识别值得长期保存的故事记忆，包括承诺、秘密、关系变化、角色动机、未解决事实和重要情绪转折。
        不要保存普通动作细节，也不要把临时场景描写误判为长期记忆。
        输出应包含：记忆条目、类型、重要性、关联角色或实体。
        """;

    protected override string TestInput =>
        """
        测试输入：诺亚答应艾琳会独自承担封锁钟楼的责任，即使这会让城卫怀疑他背叛。
        请提取值得长期保存的记忆。
        """;
}
