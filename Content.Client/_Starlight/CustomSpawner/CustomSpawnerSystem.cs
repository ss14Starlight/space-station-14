using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.CustomSpawner;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.CustomSpawner;

/// Handles client visuals related to custom spawners. Main code is in <see cref="SharedCustomSpawnerSystem"/>
public sealed partial class CustomSpawnerSystem : SharedCustomSpawnerSystem
{
    private static readonly string _shader = "Hologram";

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomSpawnerHologramComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CustomSpawnerHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    protected override void UpdateHologram(Entity<CustomSpawnerComponent> ent, Entity<CustomSpawnerHologramComponent> holo)
    {
        base.UpdateHologram(ent, holo);
        // update sprite layers for pad
        if (TryComp<SpriteComponent>(ent, out var padSprite))
        {
            _sprite.LayerSetVisible((ent, padSprite), "enabled", ent.Comp.Enabled);
            var color = Color.InterpolateBetween(ent.Comp.HologramColor1, ent.Comp.HologramColor2, 0.5f);
            if (ent.Comp.Enabled)
            {
                var animColor = Color.InterpolateBetween(color, Color.White, 0.5f);
                _sprite.LayerSetColor((ent, padSprite), "overlay", color);
                _sprite.LayerSetVisible((ent, padSprite), "overlay_anim", true);
                _sprite.LayerSetColor((ent, padSprite), "overlay_anim", animColor);
            }
            else
            {
                color = Color.InterpolateBetween(color, Color.Gray, 0.8f);
                _sprite.LayerSetColor((ent, padSprite), "overlay", color);
                _sprite.LayerSetVisible((ent, padSprite), "overlay_anim", false);
            }
        }
        // update sprite layers for holo
        if (!TryComp<SpriteComponent>(holo, out var holoSprite)) return;
        _sprite.SetVisible((holo, holoSprite), ent.Comp is { HologramVisible: true, Enabled: true });
        UpdateHologramSprite(holo.Owner, holo.Comp);
    }

    private void OnStartup(Entity<CustomSpawnerHologramComponent> ent, ref ComponentStartup args)
    {
        UpdateHologramSprite(ent, ent.Comp);
    }

    private void OnShaderRender(Entity<CustomSpawnerHologramComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Sprite.PostShader is null) return;
        UpdateHologramSprite(ent, ent.Comp);
    }

    private void UpdateHologramSprite(EntityUid uid, CustomSpawnerHologramComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        for (var i = sprite.AllLayers.Count() - 1; i >= 0; i--)
            _sprite.RemoveLayer((uid, sprite), i);

        var hologramLayer = new PrototypeLayerData
        {
            RsiPath = comp.Rsi,
            State = comp.State,
        };
        _sprite.AddLayer((uid, sprite), hologramLayer, null);
        _sprite.SetColor((uid, sprite), Color.White);
        _sprite.SetDrawDepth((uid, sprite), 1);

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
            if (_sprite.TryGetLayer((uid, sprite), i, out var layer, false) && layer.ShaderPrototype != "DisplacedDraw")
                sprite.LayerSetShader(i, "unshaded");

        UpdateHologramShader(comp, sprite);
    }

    private void UpdateHologramShader(CustomSpawnerHologramComponent comp, SpriteComponent sprite)
    {
        // Find the texture height of the largest layer
        float texHeight = sprite.AllLayers.Max(x => x.PixelSize.Y);

        var instance = _proto.Index<ShaderPrototype>(_shader).InstanceUnique();
        instance.SetParameter("color1", new Vector3(comp.Color1.R, comp.Color1.G, comp.Color1.B));
        instance.SetParameter("color2", new Vector3(comp.Color2.R, comp.Color2.G, comp.Color2.B));
        instance.SetParameter("alpha", comp.Alpha);
        instance.SetParameter("intensity", comp.Intensity);
        instance.SetParameter("texHeight", texHeight);
        instance.SetParameter("t", (float)_timing.CurTime.TotalSeconds * comp.ScrollRate);

        sprite.PostShader = instance;
        sprite.RaiseShaderEvent = true;
    }
}
