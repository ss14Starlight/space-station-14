using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Lobby.UI;

/// <summary>
/// Edits the flat <see cref="HumanoidCharacterAppearance"/>: a fixed part tree, where each part shows the
/// marking categories drawn on it, with the species' marking points as limits.
/// </summary>
public sealed class HumanoidBodyEditorBackend : IBodyEditorBackend
{
    private const string HairGroup = "hair";
    private const string FacialHairGroup = "facialhair";
    private const string SkinColor = "skin";
    private const string EyeColor = "eyes";

    private sealed record PartDef(
        string Id,
        LocId Name,
        HumanoidVisualLayers[] Layers,
        MarkingCategories[] Categories,
        PartDef[] Children);

    // Arms and legs share a category per pair of limbs, so a limb only takes the markings drawn on its own layer.
    private static readonly PartDef _body = new("torso", "body-editor-part-torso",
        [
            HumanoidVisualLayers.Chest, HumanoidVisualLayers.Tail, HumanoidVisualLayers.TailExtras, HumanoidVisualLayers.TailOverlay,
            HumanoidVisualLayers.UndergarmentTop, HumanoidVisualLayers.UndergarmentBottom, HumanoidVisualLayers.BodyCover, HumanoidVisualLayers.BodyCoverCover
        ],
        [
            MarkingCategories.Chest, MarkingCategories.Tail, MarkingCategories.TailExtras, MarkingCategories.UndergarmentTop,
            MarkingCategories.UndergarmentBottom, MarkingCategories.BodyCover, MarkingCategories.BodyCoverCover, MarkingCategories.Overlay
        ],
        [
            new("head", "body-editor-part-head",
                [
                    HumanoidVisualLayers.Head, HumanoidVisualLayers.Hair, HumanoidVisualLayers.FacialHair, HumanoidVisualLayers.Snout,
                    HumanoidVisualLayers.SnoutCover, HumanoidVisualLayers.HeadSide, HumanoidVisualLayers.HeadTop, HumanoidVisualLayers.Eyes,
                    HumanoidVisualLayers.FaceCover, HumanoidVisualLayers.FaceCoverCover],
                [
                    MarkingCategories.Hair, MarkingCategories.FacialHair, MarkingCategories.Head, MarkingCategories.HeadTop,
                    MarkingCategories.HeadSide, MarkingCategories.Eyes, MarkingCategories.Snout, MarkingCategories.SnoutCover,
                    MarkingCategories.FaceCover, MarkingCategories.FaceCoverCover],
                []),
            Limb("rarm", "body-editor-part-right-arm", HumanoidVisualLayers.RArm, MarkingCategories.Arms,
                Limb("rhand", "body-editor-part-right-hand", HumanoidVisualLayers.RHand, MarkingCategories.Arms)),
            Limb("larm", "body-editor-part-left-arm", HumanoidVisualLayers.LArm, MarkingCategories.Arms,
                Limb("lhand", "body-editor-part-left-hand", HumanoidVisualLayers.LHand, MarkingCategories.Arms)),
            Limb("rleg", "body-editor-part-right-leg", HumanoidVisualLayers.RLeg, MarkingCategories.Legs,
                Limb("rfoot", "body-editor-part-right-foot", HumanoidVisualLayers.RFoot, MarkingCategories.Legs)),
            Limb("lleg", "body-editor-part-left-leg", HumanoidVisualLayers.LLeg, MarkingCategories.Legs,
                Limb("lfoot", "body-editor-part-left-foot", HumanoidVisualLayers.LFoot, MarkingCategories.Legs)),
        ]);

    private static PartDef Limb(string id, LocId name, HumanoidVisualLayers layer, MarkingCategories category, params PartDef[] children)
        => new(id, name, [layer], [category], children);

    private readonly IPrototypeManager _prototype;
    private readonly MarkingManager _markings;
    private readonly Dictionary<string, PartDef> _defs = [];
    private readonly Dictionary<string, BodyEditorPart> _parts = [];

    private readonly Func<HumanoidCharacterProfile?> _getProfile;
    private MarkingSet _set = new();
    private Dictionary<MarkingCategories, MarkingPoints> _points = [];

    public event Action? Changed;

    /// <summary>
    /// Raised with the edited appearance after each edit made through the tab.
    /// </summary>
    public event Action<HumanoidCharacterAppearance>? AppearanceEdited;

    public BodyEditorPart? Root { get; }

    /// <param name="profile">The profile being edited. Read on every edit, so changes made elsewhere in the editor stay.</param>
    public HumanoidBodyEditorBackend(IPrototypeManager prototype, MarkingManager markings, Func<HumanoidCharacterProfile?> profile)
    {
        _prototype = prototype;
        _markings = markings;
        _getProfile = profile;
        Root = Build(_body);
    }

    private BodyEditorPart Build(PartDef def)
    {
        var part = new BodyEditorPart(def.Id, def.Name, [.. def.Children.Select(Build)]);
        _defs[def.Id] = def;
        _parts[def.Id] = part;
        return part;
    }

    private HumanoidCharacterProfile? _profile => _getProfile();
    private HumanoidCharacterAppearance? _appearance => _profile?.Appearance;

    /// <summary>
    /// Reloads the markings, after the profile was replaced or its species or sex changed.
    /// </summary>
    public void Reload()
    {
        var profile = _profile;
        _set = new MarkingSet();
        _points = [];

        if (profile != null && _prototype.TryIndex(profile.Species, out var species))
        {
            _set = new MarkingSet(profile.Appearance.Markings, species.MarkingPoints, _markings, _prototype);
            _set.EnsureSpecies(profile.Species, profile.Appearance.SkinColor, _markings, _prototype);
            _set.EnsureSexes(profile.Sex, _markings);
            _set.EnsureValid(_markings);

            if (_prototype.TryIndex(species.MarkingPoints, out var points))
                _points = points.Points;
        }

        Changed?.Invoke();
    }

    public IReadOnlyList<BodyEditorGroup> GetGroups(BodyEditorPart part)
    {
        var groups = new List<BodyEditorGroup>();
        if (_profile == null || !_defs.TryGetValue(part.Id, out var def))
            return groups;

        var loc = IoCManager.Resolve<ILocalizationManager>();

        foreach (var category in def.Categories)
        {
            var group = category switch
            {
                MarkingCategories.Hair => new BodyEditorGroup(HairGroup, CategoryName(loc, category), 1, 0),
                MarkingCategories.FacialHair => new BodyEditorGroup(FacialHairGroup, CategoryName(loc, category), 1, 0),
                _ => new BodyEditorGroup($"{part.Id}|{category}", CategoryName(loc, category),
                    _points.TryGetValue(category, out var points) ? points.Points : -1,
                    _points.TryGetValue(category, out points) && points.Required ? 1 : 0),
            };

            if (GetAvailable(group).Count > 0 || GetMarkings(group).Count > 0)
                groups.Add(group);
        }

        return groups;
    }

    private static string CategoryName(ILocalizationManager loc, MarkingCategories category)
        => loc.TryGetString($"markings-category-{category}", out var name) ? name : category.ToString();

    public IReadOnlyList<Marking> GetMarkings(BodyEditorGroup group)
        => _appearance == null
        ? []
        : (IReadOnlyList<Marking>)(group.Id switch
        {
            HairGroup => _appearance.HairStyleId == HairStyles.DefaultHairStyle
                                ? []
                                : [new(_appearance.HairStyleId, [_appearance.HairColor], _appearance.HairGlowing)],
            FacialHairGroup => _appearance.FacialHairStyleId == HairStyles.DefaultFacialHairStyle
                                ? []
                                : new List<Marking> { new(_appearance.FacialHairStyleId, [_appearance.FacialHairColor], _appearance.FacialHairGlowing) },
            _ => !TryParseGroup(group, out var def, out var category) || !_set.TryGetCategory(category, out var markings)
                        ? []
                        : [.. markings.Where(m => OnPart(def, m))],
        });

    public IReadOnlyList<MarkingPrototype> GetAvailable(BodyEditorGroup group)
    {
        if (_profile == null)
            return [];

        var category = group.Id switch
        {
            HairGroup => MarkingCategories.Hair,
            FacialHairGroup => MarkingCategories.FacialHair,
            _ => TryParseGroup(group, out _, out var parsed) ? parsed : MarkingCategories.Special,
        };
        if (category == MarkingCategories.Special)
            return [];

        var onlyWhitelisted = _points.TryGetValue(category, out var points) && points.OnlyWhitelisted;
        var def = _defs.GetValueOrDefault(group.Id.Split('|')[0]);
        return [.. _markings.MarkingsByCategoryAndSpeciesAndSex(category, _profile.Species, _profile.Sex).Values
            .Where(m => !onlyWhitelisted || m.SpeciesRestrictions?.Contains(_profile.Species) == true)
            .Where(m => def == null || def.Categories.Length > 1 || def.Layers.Contains(m.BodyPart))
            .OrderBy(m => Loc.GetString($"marking-{m.ID}"))];
    }

    public void ToggleMarking(BodyEditorGroup group, MarkingPrototype marking)
    {
        if (_appearance == null)
            return;

        switch (group.Id)
        {
            case HairGroup:
                Apply(_appearance.WithHairStyleName(_appearance.HairStyleId == marking.ID ? HairStyles.DefaultHairStyle : marking.ID));
                return;
            case FacialHairGroup:
                Apply(_appearance.WithFacialHairStyleName(_appearance.FacialHairStyleId == marking.ID ? HairStyles.DefaultFacialHairStyle : marking.ID));
                return;
        }

        if (!TryParseGroup(group, out _, out var category))
            return;

        if (_set.TryGetMarking(category, marking.ID, out _))
        {
            RemoveMarking(group, marking.ID);
            return;
        }

        // A full group swaps its oldest marking out, so picking a different one never needs a remove first.
        if (_set.PointsLeft(category) == 0)
        {
            var oldest = GetMarkings(group)[0] ?? _set.Markings[category].FirstOrDefault();
            if (oldest == null)
                return;
            _set.Remove(category, oldest.MarkingId);
        }

        _set.AddBack(category, CreateMarking(marking));
        ApplyMarkings();
    }

    public IReadOnlyList<Color> GetDefaultColors(MarkingPrototype marking)
        => CreateMarking(marking).MarkingColors;

    private Marking CreateMarking(MarkingPrototype prototype)
    {
        var marking = prototype.AsMarking();
        if (_profile == null || _appearance == null)
            return marking;

        if (_markings.MustMatchSkin(_profile.Species, prototype.BodyPart, out _, _prototype))
        {
            for (var i = 0; i < prototype.ColorSlotCount; i++)
                marking.SetColor(i, _appearance.SkinColor);
            return marking;
        }

        // Hair is kept apart from the marking set, but colors can follow it.
        var context = new MarkingSet(_set);
        foreach (var hair in GetMarkings(new BodyEditorGroup(HairGroup, string.Empty, 1, 0)))
            context.AddBack(MarkingCategories.Hair, hair);
        foreach (var hair in GetMarkings(new BodyEditorGroup(FacialHairGroup, string.Empty, 1, 0)))
            context.AddBack(MarkingCategories.FacialHair, hair);

        var colors = MarkingColoring.GetMarkingLayerColors(prototype, _appearance.SkinColor, _appearance.EyeColor, context);
        for (var i = 0; i < colors.Count; i++)
            marking.SetColor(i, colors[i]);
        return marking;
    }

    public void RemoveMarking(BodyEditorGroup group, string markingId)
    {
        if (_appearance == null)
            return;

        switch (group.Id)
        {
            case HairGroup:
                Apply(_appearance.WithHairStyleName(HairStyles.DefaultHairStyle));
                return;
            case FacialHairGroup:
                Apply(_appearance.WithFacialHairStyleName(HairStyles.DefaultFacialHairStyle));
                return;
        }

        if (!TryParseGroup(group, out _, out var category))
            return;

        if (GetMarkings(group).Count <= group.Min)
            return;

        _set.Remove(category, markingId);
        ApplyMarkings();
    }

    public void MoveMarking(BodyEditorGroup group, string markingId, string targetId)
    {
        if (!TryParseGroup(group, out _, out var category) || !_set.Markings.TryGetValue(category, out var list))
            return;

        var from = list.FindIndex(m => m.MarkingId == markingId);
        var to = list.FindIndex(m => m.MarkingId == targetId);
        if (from < 0 || to < 0 || from == to)
            return;

        var marking = list[from];
        list.RemoveAt(from);
        list.Insert(to, marking);
        ApplyMarkings();
    }

    public IReadOnlyList<BodyEditorColor> GetColors(BodyEditorPart part)
    {
        var colors = new List<BodyEditorColor>();
        if (_profile == null || _appearance == null || !_prototype.TryIndex(_profile.Species, out var species))
            return colors;

        // Skin is body-wide, so every part shows it.
        var strategy = _prototype.Index(species.SkinColoration).Strategy;
        colors.Add(strategy.InputType == SkinColorationStrategyInput.Unary
            ? new BodyEditorColor(SkinColor, Loc.GetString("body-editor-color-skin"), _appearance.SkinColor,
                Tone: new BodyEditorTone(strategy.ToUnary, strategy.FromUnary))
            : new BodyEditorColor(SkinColor, Loc.GetString("body-editor-color-skin"), _appearance.SkinColor,
                Constrain: strategy.ClosestSkinColor));

        if (part.Contains(_parts["head"]))
        {
            colors.Add(new BodyEditorColor(EyeColor, Loc.GetString("body-editor-color-eyes"), _appearance.EyeColor,
                Glowing: _appearance.EyeGlowing));
        }

        foreach (var group in GetGroups(part))
        {
            foreach (var marking in GetMarkings(group))
                AddMarkingColors(colors, group, marking);
        }

        return colors;
    }

    private void AddMarkingColors(List<BodyEditorColor> colors, BodyEditorGroup group, Marking marking)
    {
        if (_profile == null
            || !_markings.TryGetMarking(marking, out var prototype)
            || prototype.ForcedColoring
            || _markings.MustMatchSkin(_profile.Species, prototype.BodyPart, out _, _prototype))
            return;

        var name = Loc.GetString($"marking-{prototype.ID}");
        var slots = prototype.ColorSlotCount;
        for (var slot = 0; slot < slots && slot < marking.MarkingColors.Count; slot++)
        {
            var label = slots > 1 ? $"{name} ({SlotName(prototype, slot)})" : name;
            colors.Add(new BodyEditorColor($"{group.Id}#{marking.MarkingId}#{slot}", label, marking.MarkingColors[slot],
                Glowing: slot == 0 ? marking.IsGlowing : null));
        }
    }

    private static string SlotName(MarkingPrototype prototype, int slot)
    {
        for (var i = 0; i < prototype.Sprites.Count; i++)
        {
            if (prototype.GetColorIndex(i) == slot && prototype.Sprites[i] is SpriteSpecifier.Rsi rsi)
                return rsi.RsiState;
        }

        return (slot + 1).ToString();
    }

    public void SetColor(string colorId, Color color)
    {
        if (_appearance == null)
            return;

        switch (colorId)
        {
            case SkinColor:
                Apply(_appearance.WithSkinColor(color));
                return;
            case EyeColor:
                Apply(_appearance.WithEyeColor(color));
                return;
        }

        EditMarking(colorId, (marking, slot) => marking.SetColor(slot, color), color);
    }

    public void SetGlowing(string colorId, bool glowing)
    {
        if (_appearance == null)
            return;

        if (colorId == EyeColor)
        {
            Apply(_appearance.WithEyeGlowing(glowing));
            return;
        }

        EditMarking(colorId, (marking, _) => marking.IsGlowing = glowing, glowing: glowing);
    }

    /// <summary>
    /// Changes one marking found by a color id, hair included.
    /// </summary>
    private void EditMarking(string colorId, Action<Marking, int> edit, Color? color = null, bool? glowing = null)
    {
        if (_appearance == null)
            return;

        var parts = colorId.Split('#');
        if (parts.Length != 3 || !int.TryParse(parts[2], out var slot))
            return;

        switch (parts[0])
        {
            case HairGroup:
                Apply(color is { } hairColor ? _appearance.WithHairColor(hairColor) : _appearance.WithHairGlowing(glowing ?? false));
                return;
            case FacialHairGroup:
                Apply(color is { } facialColor ? _appearance.WithFacialHairColor(facialColor) : _appearance.WithFacialHairGlowing(glowing ?? false));
                return;
        }

        if (!Enum.TryParse<MarkingCategories>(parts[0].Split('|').Last(), out var category)
            || !_set.Markings.TryGetValue(category, out var list))
            return;

        var index = list.FindIndex(m => m.MarkingId == parts[1]);
        if (index < 0)
            return;

        var marking = new Marking(list[index]);
        edit(marking, slot);
        list[index] = marking;
        ApplyMarkings();
    }

    public IEnumerable<(object Key, BodyEditorPart Part)> GetLayers()
    {
        foreach (var (id, def) in _defs)
        {
            foreach (var layer in def.Layers)
                yield return (layer, _parts[id]);
        }

        if (_appearance == null)
            yield break;

        // Marking layers are keyed "{marking}-{state}" by the humanoid appearance system.
        var markings = _set.GetForwardEnumerator()
            .Append(new Marking(_appearance.HairStyleId, 1))
            .Append(new Marking(_appearance.FacialHairStyleId, 1));
        foreach (var marking in markings)
        {
            if (!_markings.TryGetMarking(marking, out var prototype) || PartOf(prototype.BodyPart) is not { } part)
                continue;

            foreach (var sprite in prototype.Sprites)
            {
                if (sprite is SpriteSpecifier.Rsi rsi)
                    yield return ($"{prototype.ID}-{rsi.RsiState}", part);
            }
        }
    }

    private BodyEditorPart? PartOf(HumanoidVisualLayers layer)
    {
        foreach (var (id, def) in _defs)
        {
            if (def.Layers.Contains(layer))
                return _parts[id];
        }

        return Root;
    }

    private bool OnPart(PartDef def, Marking marking)
        => def.Categories.Length > 1
           || (_markings.TryGetMarking(marking, out var prototype) && def.Layers.Contains(prototype.BodyPart));

    private bool TryParseGroup(BodyEditorGroup group, out PartDef def, out MarkingCategories category)
    {
        def = _body;
        category = default;
        var split = group.Id.Split('|');
        return split.Length == 2
               && _defs.TryGetValue(split[0], out def!)
               && Enum.TryParse(split[1], out category);
    }

    private void ApplyMarkings()
    {
        if (_appearance != null)
            Apply(_appearance.WithMarkings([.. _set.GetForwardEnumerator()]));
    }

    private void Apply(HumanoidCharacterAppearance appearance)
    {
        AppearanceEdited?.Invoke(appearance);
        Changed?.Invoke();
    }
}
