// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client._Starlight.UI;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyTab
{
    private void PopulateColors()
    {
        for (var i = ColorPickerList.ChildCount - 1; i >= 1; i--)
            ColorPickerList.RemoveChild(ColorPickerList.GetChild(i));

        var selectedRoot = _store.State.SelectedPart ?? _store.State.Character.BodyRoot;
        if (selectedRoot == null || _prototype == null)
            return;

        var selectedSources = new HashSet<ProtoId<ColorAppearanceParameterPrototype>>();
        CollectColorSources(selectedRoot, selectedSources);
        if (selectedSources.Count == 0)
            return;

        var ordered = selectedSources
            .OrderBy(s => s.Id.IndexOf(ColorAppearanceParameterPrototype.PerInstanceSeparator) >= 0 ? 1 : 0)
            .ThenBy(s => s.Id);

        foreach (var source in ordered)
        {
            var baseId = source;
            string? perInstanceAddress = null;
            var sepIdx = source.Id.IndexOf(ColorAppearanceParameterPrototype.PerInstanceSeparator);
            if (sepIdx >= 0)
            {
                baseId = new ProtoId<ColorAppearanceParameterPrototype>(source.Id[..sepIdx]);
                perInstanceAddress = source.Id[(sepIdx + 1)..];
            }

            if (!_prototype.TryIndex(baseId, out var proto))
                continue;

            string label;
            if (perInstanceAddress != null)
            {
                var markingIdPart = perInstanceAddress;
                string? layerKeyPart = null;
                var addrSepIdx = perInstanceAddress.IndexOf(ColorAppearanceParameterPrototype.PerInstanceSeparator);
                if (addrSepIdx >= 0)
                {
                    markingIdPart = perInstanceAddress[..addrSepIdx];
                    layerKeyPart = perInstanceAddress[(addrSepIdx + 1)..];
                }

                label = HumanizeId(markingIdPart);
                if (_markingManager != null && _markingManager.Markings.TryGetValue(markingIdPart, out var markingProto)
                    && Loc.TryGetString($"marking-{markingProto.ID}", out var localized))
                {
                    label = localized;
                }

                if (layerKeyPart != null)
                    label = $"{label} ({layerKeyPart})";
            }
            else
            {
                label = source.Id;
            }
            ColorPickerList.AddChild(new Label { Text = label });

            var current = _store.State.BodyProfile.Parameters.TryGetValue(source, out var existing)
                ? existing
                : proto.DefaultColor;

            var picker = new StarlightColorPicker
            {
                HorizontalExpand = true,
                Color = current,
            };

            if (perInstanceAddress == null && proto.Coloration is { } coloration)
                picker.Constrain = coloration.Clamp;

            var sourceCapture = source;
            picker.OnColorChanged += newColor => OnParameterColorChanged(sourceCapture, newColor);
            ColorPickerList.AddChild(picker);
        }
    }

    private void OnParameterColorChanged(ProtoId<ColorAppearanceParameterPrototype> source, Color color)
    {
        _store.MutateBodyProfile(
            p => p.Parameters[source] = color,
            BodyEditorChange.BodyProfileColors);
        BodyProfileChanged?.Invoke(_store.State.BodyProfile);
    }

    private static void CollectColorSources(BodyEditorBodyPartState part, HashSet<ProtoId<ColorAppearanceParameterPrototype>> sink)
    {
        foreach (var source in part.ColorSources)
            sink.Add(source);
        foreach (var child in part.Children)
            CollectColorSources(child, sink);
    }

    private static string HumanizeId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;

        var sb = new System.Text.StringBuilder(id.Length + 4);
        for (var i = 0; i < id.Length; i++)
        {
            var c = id[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
