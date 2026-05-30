// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using Content.Shared._Starlight.Traits;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

// TODO is a temporary file while the old system is still running and the new one is being developed.
public sealed record BodyEditorCharacterState
{
    public bool HasProfile { get; init; }
    public bool Enabled { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Voice { get; init; } = string.Empty;
    public string SiliconVoice { get; init; } = string.Empty;
    public ProtoId<SpeciesPrototype> Species { get; init; }
    public string CustomSpecieName { get; init; } = string.Empty;
    public string ForcedPrototype { get; init; } = string.Empty;
    public int Age { get; init; }
    public Sex Sex { get; init; }
    public Gender Gender { get; init; }
    public string PhysicalDescription { get; init; } = string.Empty;
    public string PersonalityDescription { get; init; } = string.Empty;
    public string PersonalNotes { get; init; } = string.Empty;
    public string OOCNotes { get; init; } = string.Empty;
    public string Secrets { get; init; } = string.Empty;
    public string ExploitableInfo { get; init; } = string.Empty;
    public SpawnPriorityPreference SpawnPriority { get; init; }
    public IReadOnlySet<ProtoId<JobPrototype>> JobPreferences { get; init; } = new HashSet<ProtoId<JobPrototype>>();
    public IReadOnlySet<ProtoId<AntagPrototype>> AntagPreferences { get; init; } = new HashSet<ProtoId<AntagPrototype>>();
    public IReadOnlySet<ProtoId<TraitPrototype>> TraitPreferences { get; init; } = new HashSet<ProtoId<TraitPrototype>>();
    public IReadOnlyDictionary<string, RoleLoadout> Loadouts { get; init; } = new Dictionary<string, RoleLoadout>();
    public IReadOnlyList<string> Cybernetics { get; init; } = [];
    public RoleLoadout? SpeciesLoadout { get; init; }
    public Color SkinColor { get; init; }
    public Color EyeColor { get; init; }
    public Color HairColor { get; init; }
    public Color FacialHairColor { get; init; }
    public bool EyeGlowing { get; init; }
    public string HairStyleId { get; init; } = string.Empty;
    public bool HairGlowing { get; init; }
    public string FacialHairStyleId { get; init; } = string.Empty;
    public bool FacialHairGlowing { get; init; }
    public float Width { get; init; } = 1f;
    public float Height { get; init; } = 1f;
    public IReadOnlyList<Marking> Markings { get; init; } = [];
    public BodyEditorBodyPartState? BodyRoot { get; init; }

    public static BodyEditorCharacterState? FromProfile(HumanoidCharacterProfile? profile, BodyEditorBodyPartState? bodyRoot)
    {
        if (profile == null)
            return null;

        var appearance = profile.Appearance;
        return new BodyEditorCharacterState
        {
            HasProfile = true,
            Enabled = profile.Enabled,
            Name = profile.Name,
            Voice = profile.Voice,
            SiliconVoice = profile.SiliconVoice,
            Species = profile.Species,
            CustomSpecieName = profile.CustomSpecieName,
            ForcedPrototype = profile.ForcedPrototype,
            Age = profile.Age,
            Sex = profile.Sex,
            Gender = profile.Gender,
            PhysicalDescription = profile.PhysicalDescription,
            PersonalityDescription = profile.PersonalityDescription,
            PersonalNotes = profile.PersonalNotes,
            OOCNotes = profile.OOCNotes,
            Secrets = profile.Secrets,
            ExploitableInfo = profile.ExploitableInfo,
            SpawnPriority = profile.SpawnPriority,
            JobPreferences = new HashSet<ProtoId<JobPrototype>>(profile.JobPreferences),
            AntagPreferences = new HashSet<ProtoId<AntagPrototype>>(profile.AntagPreferences),
            TraitPreferences = new HashSet<ProtoId<TraitPrototype>>(profile.TraitPreferences),
            Loadouts = CopyLoadouts(profile.Loadouts),
            Cybernetics = new List<string>(profile.Cybernetics),
            SpeciesLoadout = profile.SpeciesLoadout?.Clone(),
            SkinColor = appearance.SkinColor,
            EyeColor = appearance.EyeColor,
            HairColor = appearance.HairColor,
            FacialHairColor = appearance.FacialHairColor,
            EyeGlowing = appearance.EyeGlowing,
            HairStyleId = appearance.HairStyleId,
            HairGlowing = appearance.HairGlowing,
            FacialHairStyleId = appearance.FacialHairStyleId,
            FacialHairGlowing = appearance.FacialHairGlowing,
            Width = appearance.Width,
            Height = appearance.Height,
            Markings = CopyMarkings(appearance.Markings),
            BodyRoot = ApplyMarkings(bodyRoot, appearance.Markings),
        };
    }

    public BodyEditorCharacterState WithBodyRoot(BodyEditorBodyPartState? bodyRoot)
    {
        return this with
        {
            BodyRoot = ApplyMarkings(bodyRoot, Markings),
        };
    }

    private static IReadOnlyList<Marking> CopyMarkings(IReadOnlyList<Marking> markings)
    {
        var result = new List<Marking>();
        foreach (var marking in markings)
        {
            result.Add(new Marking(marking));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, RoleLoadout> CopyLoadouts(IReadOnlyDictionary<string, RoleLoadout> loadouts)
    {
        var result = new Dictionary<string, RoleLoadout>();
        foreach (var (key, loadout) in loadouts)
        {
            result[key] = loadout.Clone();
        }

        return result;
    }

    private static BodyEditorBodyPartState? ApplyMarkings(BodyEditorBodyPartState? root, IReadOnlyList<Marking> markings)
    {
        if (root == null)
            return null;

        return ApplyMarkings(root, markings, 0).Part;
    }

    private static (BodyEditorBodyPartState Part, int Index) ApplyMarkings(BodyEditorBodyPartState part, IReadOnlyList<Marking> markings, int index)
    {
        var ownMarkings = new List<Marking>();
        if (index < markings.Count)
        {
            ownMarkings.Add(new Marking(markings[index]));
            index++;
        }

        var children = new List<BodyEditorBodyPartState>();
        foreach (var child in part.Children)
        {
            var result = ApplyMarkings(child, markings, index);
            children.Add(result.Part);
            index = result.Index;
        }

        return (part with
        {
            Markings = ownMarkings,
            Children = children,
        }, index);
    }
}
