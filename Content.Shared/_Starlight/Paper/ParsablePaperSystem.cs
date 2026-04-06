using System.Text.RegularExpressions;
using Content.Shared.Paper;

namespace Content.Shared._Starlight.Paper;

public sealed partial class ParsablePaperSystem : EntitySystem
{
    /// <summary>
    /// Does the paper fit all of the regex requirements?
    /// </summary>
    public bool IsPaperValid(EntityUid paper)
    {
        if (!TryComp<PaperComponent>(paper, out var paperComp) || !TryComp<ParsablePaperComponent>(paper, out var parsableComp)) return false;

        var content = paperComp.Content;
        foreach (var test in parsableComp.RequiredPatterns)
        {
            var rule = new Regex(test);
            if (!rule.IsMatch(content)) return false;
        }

        return true;
    }

    /// <summary>
    /// Get values from paper contents using patterns
    /// </summary>
    /// <param name="requireAll">Should all fields need to have at least one valid result?</param>
    /// <returns>Dictionary indexed by pattern name, value is list of all occurences of that pattern</returns>
    public Dictionary<string, List<string>>? GetPaperValues(EntityUid paper, bool requireAll = false)
    {
        if (!IsPaperValid(paper)) return null;
        if (!TryComp<PaperComponent>(paper, out var paperComp) || !TryComp<ParsablePaperComponent>(paper, out var parsableComp)) return null;

        string content = paperComp.Content;

        Dictionary<string, List<string>> output = new();
        foreach (var valuePattern in parsableComp.RequestedValuePatterns)
        {
            var rule = new Regex(valuePattern.Value);
            List<string> sublist = new();

            foreach (Match match in rule.Matches(content))
            {
                if(match.Groups.Count > 1) sublist.Add(match.Groups[1].Value);
            }

            if (sublist.Count > 0)
            {
                output.Add(valuePattern.Key, sublist);
            }
            else if (requireAll) return null;
        }

        return output;
    }
}
