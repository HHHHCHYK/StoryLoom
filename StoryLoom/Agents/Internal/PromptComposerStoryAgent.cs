using StoryLoom.Agents.Core;
using StoryLoom.Agents.SemanticKernel;

namespace StoryLoom.Agents.Internal;

public class PromptComposerStoryAgent : SemanticKernelStoryAgent
{
    public PromptComposerStoryAgent(SemanticKernelAgentFactory agentFactory) : base(agentFactory)
    {
    }

    public override StoryAgentType Type => StoryAgentType.PromptComposer;
    public override string Name => "PromptComposerStoryAgent";
    protected override string Description => "Combines agent findings into final generation guidance.";
    protected override string Instructions =>
        """
        你是 StoryLoom 的提示组合 Agent。
        你的职责是把多个 Agent 的发现整理为给正文生成模型使用的清晰指令。
        你不负责发明新的故事事实，也不要输出完整正文。
        输出应包含：生成目标、必须保留的信息、需要避免的问题、语气或节奏建议。
        """;

    protected override string TestInput =>
        """
        测试输入：导演建议增强背叛压力；连续性 Agent 提醒银钥匙需要交接依据；伏笔 Agent 建议保留第十三声钟响。
        请组合成正文生成指导。
        """;
}
