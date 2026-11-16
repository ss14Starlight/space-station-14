using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Items.Systems;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared._Daiquiri.Weapons.Melee;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Daiquiri.Weapons.Melee;

public sealed partial class KatanaStanceVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KatanaStanceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ItemToggleComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<KatanaStanceComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: new[] { typeof(ItemSystem) });
        SubscribeLocalEvent<KatanaStanceComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals);
    }

    private void OnStartup(Entity<KatanaStanceComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    private void OnToggled(Entity<ItemToggleComponent> ent, ref ItemToggledEvent args)
    {
        if (TryComp<KatanaStanceComponent>(ent.Owner, out var stance))
            UpdateSprite((ent.Owner, stance));
    }

    private void OnGetInhandVisuals(Entity<KatanaStanceComponent> ent, ref GetInhandVisualsEvent args)
    {
        FlipLayers(ent, args.Layers);
    }

    private void OnGetEquipmentVisuals(Entity<KatanaStanceComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        FlipLayers(ent, args.Layers);
    }

    private void FlipLayers(Entity<KatanaStanceComponent> ent, List<(string, PrototypeLayerData)> layers)
    {
        if (!ShouldFlip(ent))
            return;

        for (var i = 0; i < layers.Count; i++)
        {
            var (key, layer) = layers[i];
            if (!Matches(ent.Comp, key, layer.State))
                continue;

            layers[i] = (key, Flip(layer));
        }
    }

    private void UpdateSprite(Entity<KatanaStanceComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var flip = ShouldFlip(ent);

        for (var i = 0; i < sprite.LayerData.Count; i++)
        {
            var layer = sprite.LayerData[i];
            if (!Matches(ent.Comp, null, layer.State.Name))
                continue;

            var scale = layer.Scale;
            var x = flip ? -MathF.Abs(scale.X) : MathF.Abs(scale.X);
            _sprite.LayerSetScale((ent.Owner, sprite), i, new Vector2(x, scale.Y));
        }
    }

    private bool ShouldFlip(Entity<KatanaStanceComponent> ent)
    {
        return ent.Comp.FlipSpriteInDefense &&
               TryComp<ItemToggleComponent>(ent.Owner, out var toggle) &&
               toggle.Activated;
    }

    private static bool Matches(KatanaStanceComponent comp, string? key, string? state)
    {
        if (comp.FlipSpriteLayers.Count == 0)
            return true;

        return key != null && comp.FlipSpriteLayers.Contains(key) ||
               state != null && comp.FlipSpriteLayers.Contains(state);
    }

    private static PrototypeLayerData Flip(PrototypeLayerData layer)
    {
        var scale = layer.Scale ?? Vector2.One;

        return new PrototypeLayerData
        {
            Shader = layer.Shader,
            TexturePath = layer.TexturePath,
            RsiPath = layer.RsiPath,
            State = layer.State,
            Scale = new Vector2(-scale.X, scale.Y),
            Rotation = layer.Rotation,
            Offset = layer.Offset,
            Visible = layer.Visible,
            Color = layer.Color,
            MapKeys = layer.MapKeys,
            RenderingStrategy = layer.RenderingStrategy,
            CopyToShaderParameters = layer.CopyToShaderParameters,
            Cycle = layer.Cycle,
            Loop = layer.Loop,
        };
    }
}
