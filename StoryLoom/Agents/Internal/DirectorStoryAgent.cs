using StoryLoom.Agents.Core;
using StoryLoom.Agents.SemanticKernel;

namespace StoryLoom.Agents.Internal;

public class DirectorStoryAgent : SemanticKernelStoryAgent
{
    public DirectorStoryAgent(SemanticKernelAgentFactory agentFactory) : base(agentFactory)
    {
    }

    public override StoryAgentType Type => StoryAgentType.Director;
    public override string Name => "DirectorStoryAgent";
    protected override string Description => "Plans narrative direction, dramatic pressure, and next-step intent.";
    protected override string Instructions =>
        """
        你是 StoryLoom 的导演 Agent。
        你的职责是分析当前故事局面，判断下一步叙事应该增强什么：冲突、情绪、悬念、角色选择或节奏。
        你不负责直接续写正文，不要替代作者生成完整章节。
        输出应简洁，包含：当前叙事判断、下一步推进建议、需要避免的问题。
        """;

    protected override string TestInput =>
        """
        测试输入：主角刚发现盟友隐瞒了关键情报，但外部危机正在逼近。
        请给出导演 Agent 的下一步叙事建议。
        """;
}
