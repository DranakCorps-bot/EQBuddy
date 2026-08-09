using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

public static class WatchAlertText
{
    public static string MatchLabel(TrackedRule rule, TrackedRuleResult result, int delta)
    {
        var item = result.LastItem ?? "match";
        var label = rule.Kind == WatchKind.SpellFade ? SpellFadeLabel(item) : item;
        return delta > 1 ? $"{label} x{delta}" : label;
    }

    private static string SpellFadeLabel(string item)
    {
        var open = item.LastIndexOf(" (", StringComparison.Ordinal);
        if (open > 0 && item.EndsWith(")", StringComparison.Ordinal))
        {
            var spell = item[..open];
            var target = item[(open + 2)..^1];
            if (target.Length > 0)
                return $"{spell} faded off {target}";
        }

        return $"{item} faded off you";
    }
}
