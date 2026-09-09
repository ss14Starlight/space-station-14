using System.Numerics;
using Content.Client.DisplacementMap;
using Content.Shared.CCVar;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Humanoid;

public sealed partial class HumanoidAppearanceSystem : SharedHumanoidAppearanceSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private MarkingManager _markingManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, MapInitEvent>(OnMapInit); // Starlight
        SubscribeLocalEvent<HumanoidAppearanceComponent, AfterAutoHandleStateEvent>(OnHandleState);
        Subs.CVar(_configurationManager, CCVars.AccessibilityClientCensorNudity, OnCvarChanged, true);
        Subs.CVar(_configurationManager, CCVars.AccessibilityServerCensorNudity, OnCvarChanged, true);
    }

    //Starlight begin
    private void OnMapInit(Entity<HumanoidAppearanceComponent> entity, ref MapInitEvent ev) =>
        UpdateSprite((entity, entity.Comp, Comp<SpriteComponent>(entity)));
    //Starlight end

    private void OnHandleState(EntityUid uid, HumanoidAppearanceComponent component, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite((uid, component, Comp<SpriteComponent>(uid)));
    }

    private void OnCvarChanged(bool value)
    {
        var humanoidQuery = AllEntityQuery<HumanoidAppearanceComponent, SpriteComponent>();
        while (humanoidQuery.MoveNext(out var uid, out var humanoidComp, out var spriteComp))
        {
            UpdateSprite((uid, humanoidComp, spriteComp));
        }
    }

    public void UpdateSprite(Entity<HumanoidAppearanceComponent, SpriteComponent> entity) // Starlight-edit: Make public so things like tippy can force this
    {
        UpdateLayers(entity);
        ApplyMarkingSet(entity);

        var humanoidAppearance = entity.Comp1;
        var sprite = entity.Comp2;
        var species = _prototypeManager.Index(humanoidAppearance.Species);
        var speciesDisplacement = GetSpeciesDisplacement(species);

        sprite[_sprite.LayerMapReserve((entity.Owner, sprite), HumanoidVisualLayers.Eyes)].Color = humanoidAppearance.EyeColor;
        //starlight start
        if (humanoidAppearance.EyeGlowing)
            sprite.LayerSetShader(HumanoidVisualLayers.Eyes, speciesDisplacement != null ? "DisplacedDrawUnshaded" : "unshaded");
        else if (_sprite.LayerMapTryGet((entity.Owner, sprite), HumanoidVisualLayers.Eyes, out var layerIndex, true))
        {
            if (speciesDisplacement?.ShaderOverride is { } shader)
                sprite.LayerSetShader(layerIndex, shader);
            else
                sprite.LayerSetShader(layerIndex, (ShaderInstance?)null);
        }

        if (speciesDisplacement != null)
        {
            // The displacement supplies the species' default silhouette. Keep character size customization relative
            // to that default so species are not scaled down a second time after being displaced.
            var height = humanoidAppearance.Height / species.DefaultHeight;
            var width = humanoidAppearance.Width / species.DefaultWidth;
            sprite.Scale = new Vector2(width * height, height);
        }
        else
        {
            sprite.Scale = new Vector2(humanoidAppearance.Width * humanoidAppearance.Height, humanoidAppearance.Height);
        }
        //starlight end
    }

    private DisplacementData? GetSpeciesDisplacement(SpeciesPrototype species)
    {
        return ProtoMan.Resolve(species.Displacement, out var displacement)
            ? displacement.Displacement
            : null;
    }

    private static bool IsHidden(HumanoidAppearanceComponent humanoid, HumanoidVisualLayers layer)
        => humanoid.HiddenLayers.ContainsKey(layer) || humanoid.PermanentlyHidden.Contains(layer);

    private void UpdateLayers(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var component = entity.Comp1;
        var sprite = entity.Comp2;

        var oldLayers = new HashSet<HumanoidVisualLayers>(component.BaseLayers.Keys);
        component.BaseLayers.Clear();

        // add default species layers
        var speciesProto = _prototypeManager.Index(component.Species);
        var baseSprites = _prototypeManager.Index(speciesProto.SpriteSet);
        var displacement = GetSpeciesDisplacement(speciesProto);
        foreach (var (key, id) in baseSprites.Sprites)
        {
            oldLayers.Remove(key);
            if (!component.CustomBaseLayers.ContainsKey(key))
                SetLayerData(entity, key, id, displacement, sexMorph: true);
        }

        // add custom layers
        foreach (var (key, info) in component.CustomBaseLayers)
        {
            oldLayers.Remove(key);
            SetLayerData(entity, key, info.Id, displacement, sexMorph: false, color: info.Color);
        }

        // hide old layers
        // TODO maybe just remove them altogether?
        foreach (var key in oldLayers)
        {
            if (_sprite.LayerMapTryGet((entity.Owner, sprite), key, out var index, false))
                sprite[index].Visible = false;

            if (_displacement.EnsureDisplacementIsNotOnSprite((entity.Owner, sprite), key))
                sprite.LayerSetShader(key, (ShaderInstance?)null);
        }
    }

    private void SetLayerData(
        Entity<HumanoidAppearanceComponent, SpriteComponent> entity,
        HumanoidVisualLayers key,
        string? protoId,
        DisplacementData? displacement,
        bool sexMorph = false,
        Color? color = null)
    {
        var component = entity.Comp1;
        var sprite = entity.Comp2;

        var layerIndex = _sprite.LayerMapReserve((entity.Owner, sprite), key);
        var layer = sprite[layerIndex];
        layer.Visible = !IsHidden(component, key);

        if (color != null)
            layer.Color = color.Value;

        if (protoId == null)
        {
            if (_displacement.EnsureDisplacementIsNotOnSprite((entity.Owner, sprite), key))
                sprite.LayerSetShader(key, (ShaderInstance?)null);
            return;
        }

        if (sexMorph)
            protoId = HumanoidVisualLayersExtension.GetSexMorph(key, component.Sex, protoId);

        var proto = _prototypeManager.Index<HumanoidSpeciesSpriteLayer>(protoId);
        component.BaseLayers[key] = proto;

        if (proto.MatchSkin)
            layer.Color = component.SkinColor.WithAlpha(proto.LayerAlpha);

        if (proto.BaseSprite != null)
        {
            _sprite.LayerSetSprite((entity.Owner, sprite), layerIndex, proto.BaseSprite);

            if (displacement != null)
            {
                _displacement.TryAddDisplacement(displacement, (entity.Owner, sprite), layerIndex, key, out _);
                return;
            }
        }

        if (_displacement.EnsureDisplacementIsNotOnSprite((entity.Owner, sprite), key))
            sprite.LayerSetShader(key, (ShaderInstance?)null);
    }

    /// <summary>
    ///     Loads a profile directly into a humanoid.
    /// </summary>
    /// <param name="uid">The humanoid entity's UID</param>
    /// <param name="profile">The profile to load.</param>
    /// <param name="humanoid">The humanoid entity's humanoid component.</param>
    /// <remarks>
    ///     This should not be used if the entity is owned by the server. The server will otherwise
    ///     override this with the appearance data it sends over.
    /// </remarks>
    public override void LoadProfile(EntityUid uid, HumanoidCharacterProfile? profile, HumanoidAppearanceComponent? humanoid = null)
    {
        if (profile == null)
            return;

        if (!Resolve(uid, ref humanoid))
        {
            return;
        }

        if (!humanoid.AllowProfileOverride) return; //Starlight

        var customBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>();

        var speciesPrototype = _prototypeManager.Index<SpeciesPrototype>(profile.Species);
        var markings = new MarkingSet(speciesPrototype.MarkingPoints, _markingManager, _prototypeManager);

        // Add markings that doesn't need coloring. We store them until we add all other markings that doesn't need it.
        var markingFColored = new Dictionary<Marking, MarkingPrototype>();
        foreach (var marking in profile.Appearance.Markings)
        {
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                if (!prototype.ForcedColoring)
                {
                    markings.AddBack(prototype.MarkingCategory, marking);
                }
                else
                {
                    markingFColored.Add(marking, prototype);
                }
            }
        }

        // legacy: remove in the future?
        //markings.RemoveCategory(MarkingCategories.Hair);
        //markings.RemoveCategory(MarkingCategories.FacialHair);

        // We need to ensure hair before applying it or coloring can try depend on markings that can be invalid
        var hairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.Hair, out var hairAlpha, _prototypeManager)
            ? profile.Appearance.SkinColor.WithAlpha(hairAlpha)
            : profile.Appearance.HairColor.WithAlpha(hairAlpha);
        var hair = new Marking(profile.Appearance.HairStyleId,
            new[] { hairColor }, profile.Appearance.HairGlowing); //starlight

        var facialHairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.FacialHair, out var facialHairAlpha, _prototypeManager)
            ? profile.Appearance.SkinColor.WithAlpha(facialHairAlpha)
            : profile.Appearance.FacialHairColor;
        var facialHair = new Marking(profile.Appearance.FacialHairStyleId,
            new[] { facialHairColor }, profile.Appearance.FacialHairGlowing); //starlight

        if (_markingManager.CanBeApplied(profile.Species, profile.Sex, hair, _prototypeManager))
        {
            markings.AddBack(MarkingCategories.Hair, hair);
        }
        if (_markingManager.CanBeApplied(profile.Species, profile.Sex, facialHair, _prototypeManager))
        {
            markings.AddBack(MarkingCategories.FacialHair, facialHair);
        }

        // Finally adding marking with forced colors
        foreach (var (marking, prototype) in markingFColored)
        {
            var markingColors = MarkingColoring.GetMarkingLayerColors(
                prototype,
                profile.Appearance.SkinColor,
                profile.Appearance.EyeColor,
                markings
            );
            markings.AddBack(prototype.MarkingCategory, new Marking(marking.MarkingId, markingColors, marking.IsGlowing)); //starlight, glowing
        }

        markings.EnsureSpecies(profile.Species, profile.Appearance.SkinColor, _markingManager, _prototypeManager);
        markings.EnsureSexes(profile.Sex, _markingManager);
        markings.EnsureDefault(
            profile.Appearance.SkinColor,
            profile.Appearance.EyeColor,
            _markingManager);

        DebugTools.Assert(IsClientSide(uid));

        humanoid.MarkingSet = markings;
        humanoid.PermanentlyHidden = new HashSet<HumanoidVisualLayers>();
        humanoid.HiddenLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>();
        humanoid.CustomBaseLayers = customBaseLayers;
        humanoid.Sex = profile.Sex;
        humanoid.Gender = profile.Gender;
        humanoid.Age = profile.Age;
        humanoid.Species = profile.Species;
        humanoid.SkinColor = profile.Appearance.SkinColor; //starlight
        humanoid.EyeColor = profile.Appearance.EyeColor;
        humanoid.EyeGlowing = profile.Appearance.EyeGlowing;
        humanoid.Width = profile.Appearance.Width; //starlight
        humanoid.Height = profile.Appearance.Height; //starlight

        UpdateSprite((uid, humanoid, Comp<SpriteComponent>(uid)));
    }

    // Starlight
    // Maybe this function isn't needed
    // But I didn't find a way to draw custom base layers without calling UpdateSprite() which is private to this class
    public void AddCustomBaseLayers(EntityUid uid, Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> layers, HumanoidAppearanceComponent? humanoid = null){
        if (!Resolve(uid, ref humanoid))
        {
            return;
        }

        humanoid.CustomBaseLayers = layers;
        UpdateSprite((uid, humanoid, Comp<SpriteComponent>(uid)));
    }

    private void ApplyMarkingSet(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var humanoid = entity.Comp1;
        var sprite = entity.Comp2;

        // I am lazy and I CBF resolving the previous mess, so I'm just going to nuke the markings.
        // Really, markings should probably be a separate component altogether.
        ClearAllMarkings(entity);

        var censorNudity = _configurationManager.GetCVar(CCVars.AccessibilityClientCensorNudity) ||
                           _configurationManager.GetCVar(CCVars.AccessibilityServerCensorNudity);
        // The reason we're splitting this up is in case the character already has undergarment equipped in that slot.
        var applyUndergarmentTop = censorNudity;
        var applyUndergarmentBottom = censorNudity;

        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (_markingManager.TryGetMarking(marking, out var markingPrototype))
                {
                    ApplyMarking(markingPrototype, marking.MarkingColors, marking.IsGlowing, marking.Visible, entity); //starlight, glowing
                    if (markingPrototype.BodyPart == HumanoidVisualLayers.UndergarmentTop)
                        applyUndergarmentTop = false;
                    else if (markingPrototype.BodyPart == HumanoidVisualLayers.UndergarmentBottom)
                        applyUndergarmentBottom = false;
                }
            }
        }

        humanoid.ClientOldMarkings = new MarkingSet(humanoid.MarkingSet);

        AddUndergarments(entity, applyUndergarmentTop, applyUndergarmentBottom);
    }

    private void ClearAllMarkings(Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var humanoid = entity.Comp1;
        var sprite = entity.Comp2;

        foreach (var markingList in humanoid.ClientOldMarkings.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                RemoveMarking(marking, (entity, sprite));
            }
        }

        humanoid.ClientOldMarkings.Clear();

        foreach (var markingList in humanoid.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                RemoveMarking(marking, (entity, sprite));
            }
        }
    }

    private void RemoveMarking(Marking marking, Entity<SpriteComponent> spriteEnt)
    {
        if (!_markingManager.TryGetMarking(marking, out var prototype))
            return;

        foreach (var sprite in prototype.Sprites)
        {
            if (sprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var layerId = $"{marking.MarkingId}-{rsi.RsiState}";
            if (!_sprite.LayerMapTryGet(spriteEnt.AsNullable(), layerId, out var index, false))
                continue;

            _sprite.LayerMapRemove(spriteEnt.AsNullable(), layerId);
            _sprite.RemoveLayer(spriteEnt.AsNullable(), index);

            // If this marking is one that can be displaced, we need to remove the displacement as well; otherwise
            // altering a marking at runtime can lead to the renderer falling over.
            // The Vulps must be shaved.
            // (https://github.com/space-wizards/space-station-14/issues/40135).
            if (prototype.CanBeDisplaced)
                _displacement.EnsureDisplacementIsNotOnSprite(spriteEnt, layerId);
        }
    }

    private void AddUndergarments(Entity<HumanoidAppearanceComponent, SpriteComponent> entity, bool undergarmentTop, bool undergarmentBottom)
    {
        var humanoid = entity.Comp1;

        if (undergarmentTop && humanoid.UndergarmentTop != null)
        {
            var marking = new Marking(humanoid.UndergarmentTop, new List<Color> { new Color() }, false); //starlight, glowing
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                // Markings are added to ClientOldMarkings because otherwise it causes issues when toggling the feature on/off.
                humanoid.ClientOldMarkings.Markings.Add(MarkingCategories.UndergarmentTop, new List<Marking> { marking });
                ApplyMarking(prototype, null, false, true, entity); //starlight, glowing
            }
        }

        if (undergarmentBottom && humanoid.UndergarmentBottom != null)
        {
            var marking = new Marking(humanoid.UndergarmentBottom, new List<Color> { new Color() }, false); //starlight, glowing
            if (_markingManager.TryGetMarking(marking, out var prototype))
            {
                humanoid.ClientOldMarkings.Markings.Add(MarkingCategories.UndergarmentBottom, new List<Marking> { marking });
                ApplyMarking(prototype, null, false, true, entity); //starlight, glowing
            }
        }
    }

    private void ApplyMarking(MarkingPrototype markingPrototype,
        IReadOnlyList<Color>? colors,
        bool isGlowing, //starlight
        bool visible,
        Entity<HumanoidAppearanceComponent, SpriteComponent> entity)
    {
        var humanoid = entity.Comp1;
        var sprite = entity.Comp2;

        if (!_sprite.LayerMapTryGet((entity.Owner, sprite), markingPrototype.BodyPart, out _, false))
            return;

        visible &= !IsHidden(humanoid, markingPrototype.BodyPart);
        visible &= humanoid.BaseLayers.TryGetValue(markingPrototype.BodyPart, out var setting)
           && setting.AllowsMarkings;

        var displacementData = humanoid.MarkingsDisplacement.GetValueOrDefault(markingPrototype.BodyPart)
            ?? GetSpeciesDisplacement(_prototypeManager.Index(humanoid.Species));
        var isDisplaced = visible && displacementData != null && markingPrototype.CanBeDisplaced;

        // Starlight start - allow split marking sprites to render at different humanoid layer anchors.
        var layerOverrides = markingPrototype.SpriteLayers is { Count: > 0 }
            ? markingPrototype.SpriteLayers
            : null;
        var bodyPartInsertionOffset = 0;
        // Starlight end

        for (var j = 0; j < markingPrototype.Sprites.Count; j++)
        {
            var markingSprite = markingPrototype.Sprites[j];

            if (markingSprite is not SpriteSpecifier.Rsi rsi)
                return;

            var layerId = $"{markingPrototype.ID}-{rsi.RsiState}";
            // Starlight start - sprite layers can share color slots and custom render anchors.
            var anchorLayer = markingPrototype.BodyPart;
            var colorIndex = markingPrototype.GetColorIndex(j);

            if (layerOverrides != null && j < layerOverrides.Count)
                anchorLayer = layerOverrides[j];

            if (!_sprite.LayerMapTryGet((entity.Owner, sprite), anchorLayer, out var anchorLayerIndex, false))
                continue;

            var anchoredToBodyPart = anchorLayer == markingPrototype.BodyPart;
            var insertionIndex = anchoredToBodyPart
                ? anchorLayerIndex + bodyPartInsertionOffset + 1
                : anchorLayerIndex;
            // Starlight end

            if (!_sprite.LayerMapTryGet((entity.Owner, sprite), layerId, out var markingLayer, false))
            {
                markingLayer = _sprite.AddLayer((entity.Owner, sprite), markingSprite, insertionIndex);
                _sprite.LayerMapSet((entity.Owner, sprite), layerId, markingLayer);
                _sprite.LayerSetSprite((entity.Owner, sprite), layerId, rsi);
            }

            _sprite.LayerSetVisible((entity.Owner, sprite), layerId, visible);

            if (!visible || setting == null) // this is kinda implied
            {
                if (_displacement.EnsureDisplacementIsNotOnSprite((entity.Owner, sprite), layerId))
                    sprite.LayerSetShader(layerId, (ShaderInstance?)null);

                if (anchoredToBodyPart)
                    bodyPartInsertionOffset++;
                continue;
            }

            // Okay so if the marking prototype is modified but we load old marking data this may no longer be valid
            // and we need to check the index is correct.
            // So if that happens just default to white?
            // Starlight start - color slots can be shared by multiple sprites.
            if (colors != null && colorIndex < colors.Count)
                _sprite.LayerSetColor((entity.Owner, sprite), layerId, colors[colorIndex]);
            // Starlight end
            else
                _sprite.LayerSetColor((entity.Owner, sprite), layerId, Color.White);

            if (isDisplaced)
            {
                // Use the mapped layer rather than its intended insertion position. Split markings and earlier
                // displacement layers can both move it by the time the map is inserted.
                _displacement.TryAddDisplacement(displacementData!, (entity.Owner, sprite), markingLayer, layerId, out _);
            }
            else if (_displacement.EnsureDisplacementIsNotOnSprite((entity.Owner, sprite), layerId))
            {
                sprite.LayerSetShader(layerId, (ShaderInstance?)null);
            }

            //starlight start
            if (isGlowing)
            {
                // Displacement is applied via a shader on this layer, so a displaced layer needs the
                // unshaded displacement variant - otherwise "unshaded" would overwrite it and the
                // displacement map would stop applying
                sprite.LayerSetShader(layerId, isDisplaced ? "DisplacedDrawUnshaded" : "unshaded");
            }
            //starlight end

            if (anchoredToBodyPart)
                bodyPartInsertionOffset += isDisplaced ? 2 : 1;
        }
    }

    public override void SetSkinColor(EntityUid uid, Color skinColor, bool sync = true, bool verify = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.SkinColor == skinColor)
            return;

        base.SetSkinColor(uid, skinColor, false, verify, humanoid);

        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        foreach (var (layer, spriteInfo) in humanoid.BaseLayers)
        {
            if (!spriteInfo.MatchSkin)
                continue;

            var index = _sprite.LayerMapReserve((uid, sprite), layer);
            sprite[index].Color = skinColor.WithAlpha(spriteInfo.LayerAlpha);
        }
    }

    public override void SetLayerVisibility(
        Entity<HumanoidAppearanceComponent> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? slot,
        ref bool dirty)
    {
        base.SetLayerVisibility(ent, layer, visible, slot, ref dirty);

        var sprite = Comp<SpriteComponent>(ent);
        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), layer, out var index, false))
        {
            if (!visible)
                return;
            index = _sprite.LayerMapReserve((ent.Owner, sprite), layer);
        }

        var spriteLayer = sprite[index];
        if (spriteLayer.Visible == visible)
            return;

        spriteLayer.Visible = visible;

        // I fucking hate this. I'll get around to refactoring sprite layers eventually I swear
        // Just a week away...

        foreach (var markingList in ent.Comp.MarkingSet.Markings.Values)
        {
            foreach (var marking in markingList)
            {
                if (_markingManager.TryGetMarking(marking, out var markingPrototype) && markingPrototype.BodyPart == layer)
                    ApplyMarking(markingPrototype, marking.MarkingColors, marking.IsGlowing, marking.Visible, (ent, ent.Comp, sprite)); //starlight, glowing
            }
        }
    }
}
