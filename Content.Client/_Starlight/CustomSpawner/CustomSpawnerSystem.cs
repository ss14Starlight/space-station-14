using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.CustomSpawner;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.CustomSpawner;

/// Handles client visuals related to custom spawners. Main code is in <see cref="SharedCustomSpawnerSystem"/>
public sealed partial class CustomSpawnerSystem : SharedCustomSpawnerSystem
{
    /// Color interpolation lambda
    private const float Lambda = 0.5f;
    /// Color interpolation lambda for when disabled
    private const float GrayLambda = 0.8f;

    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomSpawnerHologramComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CustomSpawnerHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
    }

    protected override void UpdateHologram(EntityUid spawner, CustomSpawnerComponent sComp, EntityUid hologram, CustomSpawnerHologramComponent hComp)
    {
        base.UpdateHologram(spawner, sComp, hologram, hComp);
        // update sprite layers for pad
        if (TryComp<SpriteComponent>(spawner, out var padSprite) && sComp.UpdatePadSprites)
        {
            _sprite.LayerSetVisible((spawner, padSprite), "enabled", sComp.Enabled);
            var color = Color.InterpolateBetween(sComp.HologramColor1, sComp.HologramColor2, Lambda);
            _sprite.LayerSetVisible((spawner, padSprite), "overlay_anim", sComp.Enabled);
            if (sComp.Enabled)
            {
                var animColor = Color.InterpolateBetween(color, Color.White, Lambda);
                _sprite.LayerSetColor((spawner, padSprite), "overlay", color);
                _sprite.LayerSetColor((spawner, padSprite), "overlay_anim", animColor);
            }
            else
            {
                color = Color.InterpolateBetween(color, Color.Gray, GrayLambda);
                _sprite.LayerSetColor((spawner, padSprite), "overlay", color);
            }
        }
        // update sprite layers for holo
        if (!TryComp<SpriteComponent>(hologram, out var holoSprite)) return;
        _sprite.SetVisible((hologram, holoSprite), sComp is { HologramVisible: true, Enabled: true });
        UpdateHologramSprite(hologram, hComp);
    }

    private void OnStartup(Entity<CustomSpawnerHologramComponent> ent, ref ComponentStartup args) =>
        UpdateHologramSprite(ent, ent.Comp);

    private void OnShaderRender(Entity<CustomSpawnerHologramComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Sprite.PostShader is null) return;
        UpdateHologramSprite(ent, ent.Comp);
    }

    private void UpdateHologramSprite(EntityUid uid, CustomSpawnerHologramComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Remove all sprite layers
        for (var i = sprite.AllLayers.Count() - 1; i >= 0; i--)
            _sprite.RemoveLayer((uid, sprite), i);

        if (comp is { UseProtoSprite: true, ProtoSprite: not null })
        {
            var spriteEntity = Spawn(comp.ProtoSprite, MapCoordinates.Nullspace);
            if (!TryComp<SpriteComponent>(spriteEntity, out var spriteComp))
            {
                QueueDel(spriteEntity);
                Log.Warning($"Prototype specified for custom spawner hologram {ToPrettyString(uid)} lacks a sprite component.");
                return;
            }
            _sprite.CopySprite((spriteEntity, spriteComp), (uid, sprite));
            QueueDel(spriteEntity);
        }
        else
        {
            var hologramLayer = new PrototypeLayerData { RsiPath = comp.Rsi, State = comp.State, };
            _sprite.AddLayer((uid, sprite), hologramLayer, null);
        }

        _sprite.SetColor((uid, sprite), Color.White);
        _sprite.SetOffset((uid, sprite), comp.Offset);
        _sprite.SetDrawDepth((uid, sprite), 1);
        sprite.NoRotation = true;
        sprite.DirectionOverride = Direction.South;
        sprite.EnableDirectionOverride = true;

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
            if (_sprite.TryGetLayer((uid, sprite), i, out var layer, false) && layer.ShaderPrototype != "DisplacedDraw")
                sprite.LayerSetShader(i, "unshaded");

        UpdateHologramShader(comp, sprite);
    }

    private void UpdateHologramShader(CustomSpawnerHologramComponent comp, SpriteComponent sprite)
    {
        // Find the texture height of the largest layer
        float texHeight = sprite.AllLayers.Max(x => x.PixelSize.Y);

        var instance = _proto.Index<ShaderPrototype>(comp.ShaderName).InstanceUnique();
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
