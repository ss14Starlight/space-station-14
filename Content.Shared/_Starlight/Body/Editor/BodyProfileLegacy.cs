// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared._Starlight.Body.Preferences;
using Content.Shared.Preferences;

namespace Content.Shared._Starlight.Body.Editor;

/// <summary>
/// Helpers for converting a legacy profile into the new hierarchical profile
/// </summary>
public static class BodyProfileLegacy
{
    public static BodyProfile FromLegacy(HumanoidCharacterProfile profile, MarkingManager markingManager)
    {
        var result = new BodyProfile();
        var appearance = profile.Appearance;
        if (appearance is null)
            return result;

        foreach (var marking in appearance.Markings)
        {
            var target = markingManager.TryGetMarking(marking, out var prototype)
                ? GetLegacyTarget(prototype.MarkingCategory)
                : [BodyPartAddress.Root];

            foreach (var path in target)
                GetOrCreate(result.Root, path).Markings.Add(new Marking(marking));
        }

        return result;
    }

    private static BodyPartPreference GetOrCreate(BodyPartPreference root, BodyPartAddress path)
    {
        var node = root;
        foreach (var segment in path.Segments)
        {
            if (!node.Children.TryGetValue(segment, out var child))
            {
                child = new BodyPartPreference();
                node.Children[segment] = child;
            }
            node = child;
        }
        return node;
    }

    private static IReadOnlyList<BodyPartAddress> GetLegacyTarget(MarkingCategories category)
        => category switch
        {
            MarkingCategories.Hair
                or MarkingCategories.FacialHair
                or MarkingCategories.Head
                or MarkingCategories.HeadTop
                or MarkingCategories.HeadSide
                or MarkingCategories.Eyes
                or MarkingCategories.Snout
                or MarkingCategories.SnoutCover => [BodyPartAddress.Root.Append("Head")],

            MarkingCategories.Arms => [BodyPartAddress.Root.Append("LeftArm"), BodyPartAddress.Root.Append("RightArm")],
            MarkingCategories.Hands => [BodyPartAddress.Root.Append("LeftArm").Append("Hand"), BodyPartAddress.Root.Append("RightArm").Append("Hand")],
            MarkingCategories.Legs => [BodyPartAddress.Root.Append("LeftLeg"), BodyPartAddress.Root.Append("RightLeg")],
            MarkingCategories.Feet => [BodyPartAddress.Root.Append("LeftLeg").Append("Foot"), BodyPartAddress.Root.Append("RightLeg").Append("Foot")],

            MarkingCategories.Tail or MarkingCategories.TailExtras => [BodyPartAddress.Root.Append("Tail")],

            _ => [BodyPartAddress.Root],
        };

    public static BodyProfile GetOrConvert(HumanoidCharacterProfile profile, MarkingManager markingManager)
        => profile.BodyEditorProfile?.Clone() ?? FromLegacy(profile, markingManager);
}
