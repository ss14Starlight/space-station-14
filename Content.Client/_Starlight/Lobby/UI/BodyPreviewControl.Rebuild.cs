// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using Content.Shared._Starlight.Body.Editor;
using Content.Shared._Starlight.Body.Prototypes;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    private void OnStateChanged(BodyEditorState state, BodyEditorChange change)
    {
        if ((change & BodyEditorChange.BodyProfileColors) != 0)
        {
            Rebuild(force: true, dispatchBodyTree: false);
            return;
        }

        if ((change & BodyEditorChange.BodyProfileMarkings) != 0)
        {
            Rebuild(force: true, dispatchBodyTree: true);
            return;
        }

        if ((change & BodyEditorChange.SelectedPart) != 0)
        {
            Rebuild(force: true, dispatchBodyTree: false);
            return;
        }

        if (!TryRefreshInPlace())
            Rebuild();
    }

    private bool TryRefreshInPlace()
    {
        if (_layerControls.Count == 0 || GetStructuralKey() != _lastStructuralKey)
            return false;

        foreach (var (control, layer) in _layerControls)
        {
            control.SetTexture(GetTexture(layer.Sprite), GetDirection(layer.Sprite));
            control.Color = GetLayerColor(layer);
        }

        _lastBuildKey = GetBuildKey();
        return true;
    }

    private void Rebuild(bool force = false, bool dispatchBodyTree = true)
    {
        if (_prototype == null || _sprite == null)
            return;

        var buildKey = GetBuildKey();
        if (_isRebuilding || (!force && buildKey == _lastBuildKey))
            return;

        _isRebuilding = true;
        try
        {
            _preview.RemoveAllChildren();
            _layerControls.Clear();

            if (!_prototype.TryIndex<BodyPrefabPrototype>(GetBodyPrefab(), out var bodyPrefab))
                return;

            var layers = new List<PreviewLayer>();
            var bodyRoot = AddPart(bodyPrefab.Root, _store?.State.BodyProfile.Root, "root", new BodyPartAddress("/root"), null, 0, layers);

            foreach (var layer in SortLayers(layers))
                AddPreviewLayer(layer);

            CenterChildren(_preview);

            _lastBuildKey = buildKey;
            _lastStructuralKey = GetStructuralKey();
            if (dispatchBodyTree)
                _store?.Dispatch(new BodyEditorSetBodyTreeAction(bodyRoot));
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private string GetBuildKey()
    {
        var direction = _store?.State.Direction ?? RsiDirection.South;
        var profileHash = _store?.State.BodyProfile is { } bp ? RuntimeHelpers.GetHashCode(bp) : 0;
        var selected = _store?.State.SelectedPartPath?.ToString() ?? string.Empty;
        return $"{GetBodyPrefab()}|{direction}|{profileHash}|{selected}";
    }

    private string GetStructuralKey()
    {
        var character = _store?.State.Character;
        var profileHash = _store?.State.BodyProfile is { } bp ? RuntimeHelpers.GetHashCode(bp) : 0;
        var species = character?.Species.Id ?? string.Empty;
        var hasProfile = character?.HasProfile == true ? 1 : 0;
        var scoped = _store?.State.SelectedPartPath?.HasMarkingSet == true ? _store?.State.SelectedPartPath?.ToString() : string.Empty;
        return $"{GetBodyPrefab()}|{hasProfile}|{species}|{profileHash}|{scoped}";
    }

    private ProtoId<BodyPrefabPrototype> GetBodyPrefab() => _store?.State.BodyPrefab ?? new BodyEditorState().BodyPrefab;
}
