using System.Collections.Generic;
using System.Linq;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Body.Preferences;

/// <summary>
/// Hierarchical player preference for a body part
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class BodyPartPreference
{
    [DataField]
    public List<Marking> Markings = [];

    /// <summary>
    /// Optional override entity prototype for this body part. <c>null</c> means use the default from the body prefab.
    /// </summary>
    [DataField]
    public EntProtoId? BodyPartOverride;

    /// <summary>
    /// Child preferences keyed by socket id
    /// </summary>
    [DataField]
    public Dictionary<string, BodyPartPreference> Children = [];

    public BodyPartPreference Clone()
    {
        var clone = new BodyPartPreference
        {
            BodyPartOverride = BodyPartOverride,
        };
        foreach (var marking in Markings)
            clone.Markings.Add(new Marking(marking));
        foreach (var (socket, child) in Children)
            clone.Children[socket] = child.Clone();
        return clone;
    }

    public bool MemberwiseEquals(BodyPartPreference? other)
    {
        if (other == null)
            return false;
        if (BodyPartOverride != other.BodyPartOverride)
            return false;
        if (!Markings.SequenceEqual(other.Markings))
            return false;
        if (Children.Count != other.Children.Count)
            return false;
        foreach (var (socket, child) in Children)
        {
            if (!other.Children.TryGetValue(socket, out var otherChild))
                return false;
            if (!child.MemberwiseEquals(otherChild))
                return false;
        }
        return true;
    }
}
