// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Body.Prototypes;

/// <summary>
/// A composite key based on a <see cref="VisualLayerPrototype"/> id with an optional numeric index.
/// The index can be used to distinguish multiple sub-layers that share the same logical layer
/// </summary>
/// <remarks>
/// String form: <c>"LayerId"</c>, <c>"LayerId@Index"</c>, <c>"LayerId-displacement"</c>,
/// or <c>"LayerId@Index-displacement"</c>.
/// </remarks>
[Serializable, NetSerializable]
public readonly record struct VisualLayerKey(
    ProtoId<VisualLayerPrototype> Layer,
    int? Index = null,
    bool Displacement = false)
{
    public const char Separator = '@';
    public const string DisplacementSuffix = "-displacement";

    public bool HasIndex => Index.HasValue;

    public static implicit operator VisualLayerKey(ProtoId<VisualLayerPrototype> layer) => new(layer);
    public static implicit operator VisualLayerKey(string layer) => Parse(layer);
    public static implicit operator string(VisualLayerKey key) => key.ToString();

    public override string ToString() => Index.HasValue
        ? string.Concat(
            Layer.Id,
            Separator.ToString(),
            Index.Value.ToString(CultureInfo.InvariantCulture),
            Displacement ? DisplacementSuffix : string.Empty)
        : string.Concat(Layer.Id, Displacement ? DisplacementSuffix : string.Empty);

    public static VisualLayerKey Parse(string value)
    {
        if (!TryParse(value, out var key))
            throw new ArgumentException($"Invalid {nameof(VisualLayerKey)} string '{value}'. Expected 'LayerId', 'LayerId{Separator}Index', 'LayerId{DisplacementSuffix}', or 'LayerId{Separator}Index{DisplacementSuffix}'.", nameof(value));
        return key;
    }

    public static bool TryParse([NotNullWhen(true)] string? value, out VisualLayerKey result)
    {
        result = default;
        if (string.IsNullOrEmpty(value))
            return false;

        var displacement = value.EndsWith(DisplacementSuffix, StringComparison.Ordinal);
        if (displacement)
            value = value[..^DisplacementSuffix.Length];

        if (string.IsNullOrEmpty(value))
            return false;

        var sepIdx = value.IndexOf(Separator);
        if (sepIdx < 0)
        {
            result = new VisualLayerKey(value, null, displacement);
            return true;
        }

        if (sepIdx == 0 || sepIdx == value.Length - 1)
            return false;

        var layerPart = value[..sepIdx];
        var indexPart = value[(sepIdx + 1)..];

        if (!int.TryParse(indexPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            return false;

        result = new VisualLayerKey(layerPart, index, displacement);
        return true;
    }
}
