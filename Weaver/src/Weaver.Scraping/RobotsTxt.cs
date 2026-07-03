namespace Weaver.Scraping;

/// <summary>Parsed robots.txt rules for one user-agent. Follows the common convention: the most
/// specific (longest) matching rule wins, Allow beats Disallow on a tie, no match means allowed.</summary>
public class RobotsRules
{
    public static readonly RobotsRules AllowAll = new(new List<(string, bool)>());

    private readonly List<(string Path, bool Allow)> _rules;

    internal RobotsRules(List<(string, bool)> rules) => _rules = rules;

    public bool IsAllowed(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        (int Length, bool Allow)? best = null;
        foreach (var (rulePath, allow) in _rules)
        {
            if (rulePath.Length == 0)
            {
                continue; // "Disallow:" with no value means allow everything -- no rule to apply.
            }

            if (!PathMatches(path, rulePath))
            {
                continue;
            }

            if (best is null || rulePath.Length > best.Value.Length || (rulePath.Length == best.Value.Length && allow))
            {
                best = (rulePath.Length, allow);
            }
        }

        return best?.Allow ?? true;
    }

    /// <summary>Prefix match with support for the two common wildcards: '*' (any chars) and a trailing '$' (end anchor).</summary>
    private static bool PathMatches(string path, string rule)
    {
        var anchored = rule.EndsWith('$');
        if (anchored)
        {
            rule = rule[..^1];
        }

        var segments = rule.Split('*');
        var position = 0;
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0)
            {
                continue;
            }

            var index = i == 0
                ? (path.StartsWith(segment, StringComparison.Ordinal) ? 0 : -1)
                : path.IndexOf(segment, position, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            position = index + segment.Length;
        }

        return !anchored || (position == path.Length && !rule.EndsWith('*'));
    }
}

public static class RobotsTxtParser
{
    /// <summary>Extracts the rules that apply to the given user-agent token: the specific group if
    /// one names it, otherwise the '*' group, otherwise allow-all.</summary>
    public static RobotsRules Parse(string content, string userAgentToken)
    {
        var specific = new List<(string, bool)>();
        var wildcard = new List<(string, bool)>();
        var currentAgents = new List<string>();
        var lastLineWasAgent = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }

            line = line.Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var directive = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            switch (directive)
            {
                case "user-agent":
                    if (!lastLineWasAgent)
                    {
                        currentAgents.Clear();
                    }

                    currentAgents.Add(value.ToLowerInvariant());
                    lastLineWasAgent = true;
                    break;

                case "allow":
                case "disallow":
                    lastLineWasAgent = false;
                    var rule = (value, directive == "allow");
                    if (currentAgents.Any(a => a != "*" && userAgentToken.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    {
                        specific.Add(rule);
                    }
                    else if (currentAgents.Contains("*"))
                    {
                        wildcard.Add(rule);
                    }

                    break;

                default:
                    lastLineWasAgent = false;
                    break;
            }
        }

        return new RobotsRules(specific.Count > 0 ? specific : wildcard);
    }
}
