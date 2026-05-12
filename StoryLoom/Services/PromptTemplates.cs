using System.Collections.Generic;
using System.Security;
using System.Text.Json;
using StoryLoom.Data.Models;

namespace StoryLoom.Services
{
    /// <summary>
    /// 提示词模板静态类。
    /// 集中管理所有发往 LLM 的提示词格式，方便统一修改和维护。
    /// </summary>
    public static class PromptTemplates
    {
        private static string EscapePromptXml(string value)
        {
            return SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
        }

        private static string FormatWritingSkill(WritingSkillSnapshot? writingSkill)
        {
            if (writingSkill == null) return string.Empty;

            return "\n\n<WritingSkill>\n" +
                $"Name: {EscapePromptXml(writingSkill.Name)}\n" +
                $"Purpose: {EscapePromptXml(writingSkill.Purpose)}\n" +
                $"StyleDescription: {EscapePromptXml(writingSkill.StyleDescription)}\n" +
                $"WritingRules: {EscapePromptXml(writingSkill.WritingRules)}\n" +
                $"WritingProcess: {EscapePromptXml(writingSkill.WritingProcess)}\n" +
                $"ForbiddenPatterns: {EscapePromptXml(writingSkill.ForbiddenPatterns)}\n" +
                $"ExampleText: {EscapePromptXml(writingSkill.ExampleText)}\n" +
                $"Notes: {EscapePromptXml(writingSkill.Notes)}\n" +
                "</WritingSkill>";
        }

        /// <summary>
        /// 用于测试连接的简单提示词。
        /// </summary>
        public static string TestConnection => "Hello, are you online? Reply entirely in Chinese with '是的，我已上线', and do not use any special characters like *, #, or \\.";

/// <summary>
        /// 生成或更新故事摘要的提示词。
        /// 针对全量归档模式优化：确保最新动作被作为当前状态捕获，以便在清空历史后仍能连贯生成。
        /// </summary>
        /// <param name="textToSummarize">需要总结的文本内容。</param>
        /// <param name="existingSummary">现有的摘要（如果有），用于增量更新。</param>
        /// <returns>构建好的提示词字符串。</returns>
        public static string Summarize(string textToSummarize, string existingSummary)
        {
            // 核心指令：强制输出格式、强制中文、禁止特殊字符、以及针对全量归档的“动作捕捉”权重
            string strictInstructions = 
                "CRITICAL INSTRUCTION: Output the summary text DIRECTLY. Do NOT include any introductory phrases, conversational filler, or concluding remarks. " +
                "LANGUAGE AND FORMAT: You MUST respond entirely in Chinese (Simplified). Do NOT use any special characters such as '*', '#', or '\\' in your output. " +
                "CONTENT PRIORITY: \n" +
                "1. PRESERVE RECENT ACTIONS: Pay special attention to the LATEST user input in the content. Ensure their current intent or action is captured as the 'immediate situation'.\n" +
                "2. CORE FACTS: Retain character states, essential items, and key world-building details.\n" +
                "3. COMPRESSION: Discard narrative transitions and filler logic to keep the summary concise but informative.";

            if (string.IsNullOrWhiteSpace(existingSummary))
            {
                return $"{strictInstructions}\n\n" +
                    $"Please summarize the following story content. Ensure the protagonist's current situation and latest actions are clearly stated:\n\n" +
                    $"<Content>\n{textToSummarize}\n</Content>";
            }
            else
            {
                return $"{strictInstructions}\n\n" +
                    $"Please merge the new content into the existing summary. \n" +
                    $"IMPORTANT: Update the 'Current Situation' based on the latest developments in the <NewContent>. The resulting summary should act as the complete context for the next turn.\n\n" +
                    $"<ExistingSummary>\n{existingSummary}\n</ExistingSummary>\n\n" +
                    $"<NewContent>\n{textToSummarize}\n</NewContent>\n\n" +
                    $"Provide a single, consolidated summary directly.";
            }
        }

        /// <summary>
        /// 用于润色和扩展文本（如背景设定、角色描述）的提示词。
        /// </summary>
        /// <param name="input">需要润色的原始文本。</param>
        /// <param name="type">文本类型（例如 "Background" 背景, "Protagonist" 主角）。</param>
        /// <returns>构建好的提示词字符串。</returns>
        public static string Enhance(string input, string type)
        {
            return "You are an expert creative writing editor specializing in immersive world-building and character design.\n" +
           $"Your task is to enhance the following '{type}' description. Make it more vivid, atmospheric, and detailed, while strictly maintaining the original core concepts.\n\n" +
           "DIRECTIVES:\n" +
           "- Language & Format: You MUST respond entirely in Chinese. Do NOT use markdown or special characters like '*', '#', or '\\'.\n" +
           "- Show, Don't Tell: Expand using sensory details (sight, sound, smell, texture) rather than just adding abstract adjectives.\n" +
           "- Tone & Atmosphere: Intelligently match the implied setting of the input. For example, if the input hints at Victorian cosmic horror, use appropriate gothic, dread-inducing, and era-accurate vocabulary.\n" +
           "- Avoid Purple Prose: Keep the prose evocative but readable. Do not over-embellish or use unnecessarily convoluted words.\n" +
           "- CRITICAL: Output ONLY the enhanced text. Do not include conversational filler like 'Here is the enhanced description'.\n\n" +
           $"<{type}>\n{input}\n</{type}>";
        }

        /// <summary>
        /// 获取后续剧情建议的提示词。
        /// </summary>
        /// <param name="actionType">用户选择的动作类型（如 "Speak", "Think", "Action"），若为 null 则自动。</param>
        public static string GetSuggestions(string? actionType)
        {
            var prompt = "Based on the current story state, provide exactly 3 distinct, short (1-2 sentences) options for the protagonist's next action.\n\n" +
            "DIRECTIVES:\n" +
            "- Variety: Ensure the options cover different approaches.\n";

            if (!string.IsNullOrEmpty(actionType) && actionType != "🎭 Actions") // "🎭 Actions" is the default label for Auto/Menu
            {
                prompt += $"- PRIORITY FOCUS: The user has explicitly chosen to '{actionType}'. ALL suggestions MUST be of this type (e.g., if 'Speak', all options must be dialogue; if 'Think', all internal monologues).\n";
            }
            else
            {
                prompt += "- Variety: Ensure the options cover different tactical or narrative approaches (e.g., one investigative, one aggressive/action-oriented, one cautious or dialogue-based).\n";
            }

            prompt += "- Tone: Keep the actions strictly in-character and aligned with the established world atmosphere.\n" +
            "- Language & Format: The options MUST be written entirely in Chinese. Do NOT use special characters like '*', '#', or '\\' inside the strings.\n\n" +
            "CRITICAL FORMATTING INSTRUCTION:\n" +
            "You MUST output ONLY a valid JSON array of strings. Do not use markdown formatting (like ```json), do not include any introductory or concluding text, and do not number the items inside the string.\n\n";

            return prompt;
        }
            
        
        /// <summary>
        /// 构建故事生成的系统提示词（System Prompt）。
        /// </summary>
        /// <param name="background">世界观背景设定。</param>
        /// <param name="protagonist">主角设定。</param>
        /// <param name="summary">之前的剧情摘要（通常是压缩后的事实和状态）。</param>
        /// <returns>构建好的系统提示词字符串。</returns>
        public static string StoryGenerationSystemPrompt(
            string background,
            string protagonist,
            string summary,
            string? actionType = null,
            WritingSkillSnapshot? activeWritingSkill = null)
        {
            // 1. 强化角色定义、行为准则、语言和格式控制
            var directives = 
         "You are an expert interactive fiction co-author. Your goal is to write immersive, engaging narrative text.\n" +
         "DIRECTIVES:\n" +
         "- Language & Format: You MUST write the story entirely in Chinese (Simplified). Do NOT use markdown formatting or special characters such as '*', '#', or '\\' in your text.\n" +
         "- Tone & Style: Strictly match the atmosphere of the World Background (e.g., maintain dread and mystery if it's horror, or formal language for historical settings).\n" +
         "- Show, Don't Tell: Drive the plot forward through character actions, sensory details, and dialogue. Avoid sounding like a wiki or summarizing.\n" +
         "- State Integration: The 'Previous Story Summary' contains current facts, inventory, and character states. Seamlessly weave these facts into the narrative context without explicitly listing them.\n" +
         "- Continuity: Ensure the protagonist's actions and internal thoughts align with their defined persona.\n" +
         "- NO Options or Choices: Do NOT generate any interactive options, questions (e.g., 'What do you do next?'), or choices at the end of the text. Output pure narrative prose only.";

            if (!string.IsNullOrEmpty(actionType) && actionType != "🎭 Actions" && actionType != "🤖 Auto")
            {
               directives += $"\n- CRITICAL ACTION CONSTRAINT: The user has explicitly chosen to '{actionType}'. You MUST focus the narrative on this action type immediately. (e.g., if 'Think', write internal monologue; if 'Speak', write dialogue; if 'Action', describe physical actions).";
            }

            // 2. 使用伪 XML 标签结构化数据
            var systemContent = 
                $"{directives}\n\n" +
                $"<WorldBackground>\n{background}\n</WorldBackground>\n\n" +
                $"<Protagonist>\n{protagonist}\n</Protagonist>";

            systemContent += FormatWritingSkill(activeWritingSkill);

            // 3. 动态追加摘要（作为当前故事状态）
            if (!string.IsNullOrWhiteSpace(summary))
            {
                systemContent += $"\n\n<CurrentStoryState_Summary>\n{summary}\n</CurrentStoryState_Summary>";
            }
    
            // 4. 最终收尾指令，聚焦下一步输出（严厉制止输出选项）
            systemContent += "\n\nCRITICAL: Respond ONLY with the continuation of the story in Chinese. Output the pure narrative text and immediately STOP. Do NOT provide any choices, options, or prompt the user. Do not break character, and do not add out-of-character commentary.";

            return systemContent;
        }

        public static string EvolveWritingSkill(WritingSkillSnapshot current, string evolutionGoal)
        {
            var currentJson = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });

            return "You are helping improve a reusable writing skill for an AI fiction writing app.\n" +
                "Return ONLY a valid JSON object matching this schema exactly:\n" +
                "{\n" +
                "  \"Name\": \"\",\n" +
                "  \"Purpose\": \"\",\n" +
                "  \"StyleDescription\": \"\",\n" +
                "  \"WritingRules\": \"\",\n" +
                "  \"WritingProcess\": \"\",\n" +
                "  \"ForbiddenPatterns\": \"\",\n" +
                "  \"ExampleText\": \"\",\n" +
                "  \"Notes\": \"\"\n" +
                "}\n\n" +
                "Do not include markdown fences, explanations, comments, or extra fields.\n" +
                "Preserve useful existing constraints unless the evolution goal explicitly changes them.\n" +
                "The result may be written in Chinese when the current skill is Chinese.\n\n" +
                $"EvolutionGoal:\n{evolutionGoal}\n\n" +
                $"CurrentWritingSkill:\n{currentJson}";
        }
    }
}
