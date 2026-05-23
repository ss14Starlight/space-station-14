// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Body.Prototypes;

[Prototype]
public sealed partial class ColorAppearanceParameterPrototype : AppearanceParameterPrototype
{
    [DataField]
    public Color DefaultColor = Color.White;

    /// <summary>
    /// Optional clamping strategy applied to user-picked colors.
    /// </summary>
    [DataField]
    public ColorStrategy? Coloration;

    [DataField]
    public bool PerInstance;

    public const char PerInstanceSeparator = '@';

    public static ProtoId<ColorAppearanceParameterPrototype> ResolveKey(
        ProtoId<ColorAppearanceParameterPrototype> source,
        ColorAppearanceParameterPrototype proto,
        string address)
        => proto.PerInstance
            ? new ProtoId<ColorAppearanceParameterPrototype>($"{source.Id}{PerInstanceSeparator}{address}")
            : source;
}
