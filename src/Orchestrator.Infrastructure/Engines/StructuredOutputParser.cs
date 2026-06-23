using System.Text.RegularExpressions;

namespace Orchestrator.Infrastructure.Engines;

/// <summary>
/// Supervisor prompts ask Claude Code to end its final message with a fenced ```json block
/// containing a structured verdict (plan / review outcome). This pulls that block out of the
/// engine's free-text final answer.
/// </summary>
public static partial class StructuredOutputParser
{
    [GeneratedRegex(@"```json\s*(?<json>\{.*?\})\s*```", RegexOptions.Singleline)]
    private static partial Regex JsonBlockRegex();

    public static string? ExtractJsonBlock(string text)
    {
        var match = JsonBlockRegex().Match(text);
        return match.Success ? match.Groups["json"].Value : null;
    }
}
