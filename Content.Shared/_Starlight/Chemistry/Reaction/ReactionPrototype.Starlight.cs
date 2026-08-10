using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Chemistry.Reaction;

public sealed partial class ReactionPrototype
{
    [DataField]
    public string? GuidebookGroup;

    [DataField]
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
