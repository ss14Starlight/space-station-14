using System.Linq;
using System.Numerics;
using Content.Client.Items;
using Content.Client.Weapons.Ranged.Components;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared._Starlight.Weapons.Ranged.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Weapons.Ranged;

public sealed partial class GunHeatSystem : SharedGunHeatSystem
{
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> _heatShader = "GunHeat";
    private static readonly ProtoId<ShaderPrototype> _inhandHeatShader = "GunHeatInhand";
    private static readonly ProtoId<ShaderPrototype> _inhandHeatDisplacedShader = "GunHeatInhandDisplaced";
    private static readonly ProtoId<ShaderPrototype> _inhandHeatDisplacedStencilShader = "GunHeatInhandDisplacedStencil";

    private const string DisplacedDrawShader = "DisplacedDraw";
    private const string DisplacedStencilDrawShader = "DisplacedStencilDraw";
    private const string DisplacementSuffix = "-displacement";

    private readonly Dictionary<EntityUid, (ShaderInstance Instance, ShaderInstance? Shader, string? Prototype)> _glowing = new();

    private readonly Dictionary<EntityUid, HeldGlow> _held = new();

    private sealed class HeldGlow(EntityUid holder, string[] layers)
    {
        public readonly EntityUid Holder = holder;
        public readonly string[] Layers = layers;

        public readonly Dictionary<string, (ShaderInstance Instance, ShaderInstance? Shader, string? Prototype)> Glowing = new();
    }

    private readonly Dictionary<EntityUid, GunHeatStatusControl> _statusControls = new();

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<GunHeatComponent> ent, ref ComponentShutdown args)
    {
        _glowing.Remove(ent);
        _held.Remove(ent);
        _statusControls.Remove(ent);
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<GunHeatComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var glow = GetGlow(ent.Comp, ent.Comp.Temperature);
        UpdateItemGlow(ent, glow);
        UpdateInhandGlow(ent, glow);

        if (_statusControls.TryGetValue(ent, out var control))
            control.Update(ent.Comp);
    }

    [SubscribeLocalEvent(after: [typeof(GunSystem)])]
    private void OnItemStatus(Entity<GunHeatComponent> ent, ref ItemStatusCollectMessage args)
    {
        var control = new GunHeatStatusControl();
        control.Update(ent.Comp);
        _statusControls[ent] = control;
        args.Controls.Add(control);
    }

    [SubscribeLocalEvent]
    private void OnHeldVisualsUpdated(Entity<GunHeatComponent> ent, ref HeldVisualsUpdatedEvent args)
    {
        if (args.RevealedLayers.Count == 0)
        {
            if (!_hands.IsHolding(args.User, ent))
                _held.Remove(ent);

            return;
        }

        var layers = args.RevealedLayers.Where(key => !key.EndsWith(DisplacementSuffix)).ToArray();
        _held[ent] = new HeldGlow(args.User, layers);
        UpdateInhandGlow(ent, GetGlow(ent.Comp, ent.Comp.Temperature));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var held in _held.Values)
        {
            if (held.Glowing.Count == 0)
                continue;

            var direction = GetMuzzleDirection(held.Holder);
            foreach (var (instance, _, _) in held.Glowing.Values)
            {
                instance.SetParameter("muzzleDir", direction);
            }
        }
    }

    private void UpdateItemGlow(Entity<GunHeatComponent> ent, float glow)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), GunVisualLayers.Base, out var layerIndex, false))
            layerIndex = 0;

        if (layerIndex >= sprite.AllLayers.Count() || sprite[layerIndex] is not SpriteComponent.Layer layer)
            return;

        if (glow <= 0f)
        {
            if (_glowing.Remove(ent, out var saved))
                sprite.LayerSetShader(layerIndex, saved.Shader, saved.Prototype);
            return;
        }

        if (!_glowing.TryGetValue(ent, out var state))
        {
            state = (_prototype.Index(_heatShader).InstanceUnique(), layer.Shader, layer.ShaderPrototype?.Id);
            _glowing[ent] = state;
            sprite.LayerSetShader(layerIndex, state.Instance, _heatShader);
        }

        state.Instance.SetParameter("heat", glow);
    }

    private void UpdateInhandGlow(Entity<GunHeatComponent> ent, float glow)
    {
        if (!_held.TryGetValue(ent, out var held) || !TryComp<SpriteComponent>(held.Holder, out var sprite))
            return;

        var direction = GetMuzzleDirection(held.Holder);

        foreach (var key in held.Layers)
        {
            if (!_sprite.LayerMapTryGet((held.Holder, sprite), key, out var index, false)
                || sprite[index] is not SpriteComponent.Layer layer)
                continue;

            if (glow <= 0f)
            {
                if (held.Glowing.Remove(key, out var original))
                    sprite.LayerSetShader(index, original.Shader, original.Prototype);
                continue;
            }

            if (!held.Glowing.TryGetValue(key, out var state))
            {
                if (GetInhandShader(layer) is not { } shader)
                    continue;

                state = (_prototype.Index(shader).InstanceUnique(), layer.Shader, layer.ShaderPrototype?.Id);
                held.Glowing[key] = state;
                sprite.LayerSetShader(index, state.Instance, shader);
            }

            state.Instance.SetParameter("heat", glow);
            state.Instance.SetParameter("muzzleDir", direction);
        }
    }

    private static ProtoId<ShaderPrototype>? GetInhandShader(SpriteComponent.Layer layer)
        => layer.ShaderPrototype?.Id switch
        {
            null when layer.Shader == null => _inhandHeatShader,
            DisplacedDrawShader => _inhandHeatDisplacedShader,
            DisplacedStencilDrawShader => _inhandHeatDisplacedStencilShader,
            _ => (ProtoId<ShaderPrototype>?) null,
        };

    private Vector2 GetMuzzleDirection(EntityUid holder)
    {
        var angle = (_transform.GetWorldRotation(holder) + _eye.CurrentEye.Rotation).Reduced().FlipPositive();

        return SpriteComponent.Layer.GetDirection(RsiDirectionType.Dir4, angle) switch
        {
            RsiDirection.North => new Vector2(0f, 1f),
            RsiDirection.East => new Vector2(1f, 0f),
            RsiDirection.West => new Vector2(-1f, 0f),
            _ => new Vector2(0f, -1f),
        };
    }
}
