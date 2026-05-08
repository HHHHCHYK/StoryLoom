using StoryLoom.Agents.Core;
using StoryLoom.Agents.SemanticKernel;

namespace StoryLoom.Agents.Internal;

public class ContinuityStoryAgent : SemanticKernelStoryAgent
{
    public ContinuityStoryAgent(SemanticKernelAgentFactory agentFactory) : base(agentFactory)
    {
    }

    public override StoryAgentType Type => StoryAgentType.Continuity;
    public override string Name => "ContinuityStoryAgent";
    protected override string Description => "Checks continuity, timeline consistency, and setting conflicts.";
    protected override string Instructions =>
        """
        你是 StoryLoom 的连续性 Agent。
        你的职责是检查剧情、时间线、角色知识、物品状态和世界规则是否存在冲突。
        你不负责修正文风，只关注逻辑一致性。
        输出应包含：发现的问题、冲突原因、严重程度、建议修正方式。
        """;

    protected override string TestInput =>
        """
        测试输入：上一幕银钥匙在艾琳手中。本幕开头诺亚已经用银钥匙打开钟楼，但没有出现交接过程。
        请检查连续性风险。
        """;
}
