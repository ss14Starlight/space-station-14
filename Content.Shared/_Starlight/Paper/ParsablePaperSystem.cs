using System.Text.RegularExpressions;
using Content.Shared.Paper;

namespace Content.Shared._Starlight.Paper;

public sealed partial class ParsablePaperSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

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
            Match match = rule.Match(content);

            if (match.Groups.Count == 0)
            {
                if (requireAll) return null;
            }
            else
            {
                List<string> sublist = new();
                for (int i = 0; i < match.Groups.Count; i++) // foreach has failed me.... if anyone knows a better way please change
                {
                    sublist.Add(match.Groups[i].Value);
                }

                output.Add(valuePattern.Key, sublist);
            }
        }

        return output;
    }
}