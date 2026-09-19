using System.Linq;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared._Starlight.Weapons.Ranged.Systems;
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

    private readonly Dictionary<EntityUid, (ShaderInstance Instance, ShaderInstance? Shader, string? Prototype)> _glowing = new();

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<GunHeatComponent> ent, ref ComponentShutdown args)
        => _glowing.Remove(ent);

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<GunHeatComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var glow = GetGlow(ent.Comp, ent.Comp.Temperature);

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
}
