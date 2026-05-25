// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed record BodyEditorBodyPartState
{
    public string SlotId { get; init; } = string.Empty;
    public BodyPartAddress Path { get; init; } = BodyPartAddress.Root;
    public IReadOnlyList<Marking> Markings { get; init; } = [];
    public IReadOnlyList<BodyEditorBodyPartState> Children { get; init; } = [];
    public IReadOnlySet<VisualLayerKey> Layers { get; init; } = new HashSet<VisualLayerKey>();
    public IReadOnlySet<ProtoId<ColorAppearanceParameterPrototype>> ColorSources { get; init; } = new HashSet<ProtoId<ColorAppearanceParameterPrototype>>();
    public IReadOnlyList<ProtoId<MarkingSetPrototype>> MarkingSets { get; init; } = [];
}
