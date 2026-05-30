// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Body.Editor;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class BodyPreviewControl
{
    private string GetLayerTooltip(PreviewLayer layer)
    {
        var partName = GetFriendlyPartName(layer.Path);
        if (layer.IsMarking && layer.MarkingId is { } markingId)
        {
            var name = Loc.GetString($"marking-{markingId}");
            if (string.IsNullOrEmpty(name) || name.StartsWith("marking-"))
                name = markingId;
            return $"{partName} — {name}";
        }

        return partName;
    }

    private static string GetFriendlyPartName(BodyPartAddress path)
    {
        string? prev = null;
        string? last = null;
        foreach (var seg in path.Segments)
        {
            prev = last;
            last = seg;
        }

        if (last == null || last == "root")
            return "Torso";

        if (last is "Hand" or "Foot")
        {
            return prev switch
            {
                "LeftArm" => last == "Hand" ? "Left Hand" : "Left Foot",
                "RightArm" => last == "Hand" ? "Right Hand" : "Right Foot",
                "LeftLeg" => "Left Foot",
                "RightLeg" => "Right Foot",
                _ => last,
            };
        }

        return last switch
        {
            "Head" => "Head",
            "LeftArm" => "Left Arm",
            "RightArm" => "Right Arm",
            "LeftLeg" => "Left Leg",
            "RightLeg" => "Right Leg",
            _ => last,
        };
    }
}
