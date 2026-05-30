using System.Collections.Generic;
using Content.Shared._Starlight.Body.Preferences;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Body.Editor;

/// <summary>
/// Character profile model for the body editor.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class BodyProfile
{
    [DataField]
    public BodyPartPreference Root = new();

    // Todo: parameters should not only be color based, but also support shader, sound, text, and numeric values.
    [DataField]
    public Dictionary<ProtoId<ColorAppearanceParameterPrototype>, Color> Parameters = [];

    public BodyProfile Clone()
    {
        var clone = new BodyProfile
        {
            Root = Root.Clone(),
        };
        foreach (var (key, value) in Parameters)
            clone.Parameters[key] = value;
        return clone;
    }

    public bool MemberwiseEquals(BodyProfile? other)
    {
        if (other == null)
            return false;
        if (!Root.MemberwiseEquals(other.Root))
            return false;
        if (Parameters.Count != other.Parameters.Count)
            return false;
        foreach (var (key, value) in Parameters)
        {
            if (!other.Parameters.TryGetValue(key, out var otherValue))
                return false;
            if (value != otherValue)
                return false;
        }
        return true;
    }
}
