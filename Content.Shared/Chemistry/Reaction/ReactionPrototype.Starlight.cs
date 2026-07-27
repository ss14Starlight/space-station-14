using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry.Reaction;

public sealed partial class ReactionPrototype
{
    [DataField("guidebookGroup")]
    public string? GuidebookGroup;

    [DataField("guidebookGroups")]
    public List<string> GuidebookGroups = [];

    public bool InGuidebookGroup(string group)
    {
        if (string.Equals(GuidebookGroup, group, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var guidebookGroup in GuidebookGroups)
        {
            if (string.Equals(guidebookGroup, group, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
