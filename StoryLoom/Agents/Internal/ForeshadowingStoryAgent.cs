using StoryLoom.Agents.Core;
using StoryLoom.Agents.SemanticKernel;

namespace StoryLoom.Agents.Internal;

public class ForeshadowingStoryAgent : SemanticKernelStoryAgent
{
    public ForeshadowingStoryAgent(SemanticKernelAgentFactory agentFactory) : base(agentFactory)
    {
    }

    public override StoryAgentType Type => StoryAgentType.Foreshadowing;
    public override string Name => "ForeshadowingStoryAgent";
    protected override string Description => "Tracks clues, unresolved hooks, mysteries, and payoff opportunities.";
    protected override string Instructions =>
        """
        你是 StoryLoom 的伏笔 Agent。
        你的职责是识别线索、悬念、未兑现承诺、可回收细节，以及未来可以安排回响的位置。
        你不负责强行制造反转，所有建议都应服务于已有材料。
        输出应包含：伏笔或线索、当前状态、可能兑现方向、风险。
        """;

    protected override string TestInput =>
        """
        测试输入：钟楼每次敲响第十三声时，城中失踪者的名字会短暂出现在钟面上。
        请分析这里的伏笔价值。
        """;
}
