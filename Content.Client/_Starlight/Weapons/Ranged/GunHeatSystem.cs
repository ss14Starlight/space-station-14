using System.Linq;
using Content.Client.Items;
using Content.Client.Weapons.Ranged.Components;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared._Starlight.Weapons.Ranged.Systems;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Weapons.Ranged;

/// <summary>
/// Makes the barrel glow from the muzzle towards the middle of the sprite as the gun heats up.
/// </summary>
public sealed partial class GunHeatSystem : SharedGunHeatSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> _heatShader = "GunHeat";
    private static readonly ProtoId<ShaderPrototype> _inhandHeatShader = "GunHeatInhand";

    private readonly Dictionary<EntityUid, (ShaderInstance Instance, ShaderInstance? Shader, string? Prototype)> _glowing = new();

    private readonly Dictionary<EntityUid, (EntityUid Holder, string[] Layers, ShaderInstance Instance)> _held = new();

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
            _held.Remove(ent);
            return;
        }

        _held[ent] = (args.User, args.RevealedLayers.ToArray(), _prototype.Index(_inhandHeatShader).InstanceUnique());
        UpdateInhandGlow(ent, GetGlow(ent.Comp, ent.Comp.Temperature));
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

        held.Instance.SetParameter("heat", glow);

        foreach (var key in held.Layers)
        {
            if (!_sprite.LayerMapTryGet((held.Holder, sprite), key, out var index, false))
                continue;

            if (glow <= 0f)
                sprite.LayerSetShader(index, null, null);
            else
                sprite.LayerSetShader(index, held.Instance, _inhandHeatShader);
        }
    }
}
