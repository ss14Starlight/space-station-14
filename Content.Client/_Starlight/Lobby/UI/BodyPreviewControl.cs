// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Client.Clickable;
using Content.Client._Starlight.Sprite;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl : BoxContainer
{
    private const float BodyScale = 8f;

    private readonly LayoutContainer _preview = new();
    private readonly IClickMapManager _clickMap;
    private readonly IComponentFactory _componentFactory;
    private readonly VisualLayerSystem _visualLayers;
    private readonly List<(BodyPartPreviewControl Control, PreviewLayer Layer)> _layerControls = new();

    private BodyEditorStore? _store;
    private IPrototypeManager? _prototype;
    private SpriteSystem? _sprite;
    private MarkingManager? _markingManager;
    private bool _isRebuilding;
    private string? _lastBuildKey;
    private string? _lastStructuralKey;

    public IReadOnlyList<BodyEditorBodyPartState> Parts => _store?.State.Parts ?? [];
    public BodyEditorBodyPartState? SelectedPart => _store?.State.SelectedPart;

    public BodyPreviewControl()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Center;

        _clickMap = IoCManager.Resolve<IClickMapManager>();
        _componentFactory = IoCManager.Resolve<IComponentFactory>();
        _visualLayers = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<VisualLayerSystem>();

        AddChild(_preview);
        AddChild(CreateRotateButtons());
    }

    public void SetStore(BodyEditorStore store)
    {
        _store?.StateChanged -= OnStateChanged;

        _store = store;
        _store.StateChanged += OnStateChanged;
        Rebuild(force: true);
    }

    public void Initialize(IPrototypeManager prototype, SpriteSystem sprite)
    {
        _prototype = prototype;
        _sprite = sprite;
        _markingManager = IoCManager.Resolve<MarkingManager>();
        Rebuild(force: true);
    }

    private readonly record struct PreviewLayer(VisualLayerKey LayerId, SpriteSpecifier Sprite, Color SpriteColor, ProtoId<ColorAppearanceParameterPrototype>? ColorSource, BodyPartAddress Path, bool Clickable, bool IsMarking = false, string? MarkingId = null);
}
