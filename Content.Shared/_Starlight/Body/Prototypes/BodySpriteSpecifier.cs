// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Components;
using Content.Shared.Starlight.Utility;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Serializable, NetSerializable]
[DataDefinition]
public sealed partial class BodySpriteSpecifier : ExtendedSpriteSpecifier
{
    /// <summary>
    /// Which color from the humanoid profile is applied to this layer.
    /// </summary>
    [DataField]
    public BodyPartColorSource ColorSource = BodyPartColorSource.None;

    public override bool Equals(object? obj)
        => obj is BodySpriteSpecifier other
            && base.Equals(other)
            && ColorSource == other.ColorSource;

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ColorSource);
}

[Serializable, NetSerializable]
public enum BodyPartColorSource : byte
{
    None,
    SkinColor,
    EyeColor,
    HairColor,
    FacialHairColor
}
