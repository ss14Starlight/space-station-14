// SPDX-FileCopyrightText: 2026 Starlight Network
// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Body.Systems;

/// <summary>
/// Hides clothing, blood and other overlays where a humanoid's limb is missing
/// </summary>
public sealed partial class LimbMaskSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly HumanoidVisualLayers[] _limbs =
    [
        HumanoidVisualLayers.LArm, HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LHand, HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LLeg, HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LFoot, HumanoidVisualLayers.RFoot,
    ];

    private static readonly ProtoId<ShaderPrototype> _maskShader = "LimbMask";
    private static readonly ProtoId<ShaderPrototype> _maskDisplacedShader = "LimbMaskDisplaced";
    private static readonly ProtoId<ShaderPrototype> _maskDisplacedStencilShader = "LimbMaskDisplacedStencil";
    private const string DisplacedDrawShader = "DisplacedDraw";
    private const string DisplacedStencilDrawShader = "DisplacedStencilDraw";

    private const string TargetKeyPrefix = "limbmask-target-";
    private const string ParamKeyPrefix = "limbmask-param-";

    private readonly Dictionary<EntityUid, MaskState> _masked = [];
    private readonly HashSet<int> _baseLayers = [];

    private sealed class MaskState
    {
        public int Missing;
        public int LayerCount;
        public readonly List<Target> Targets = [];
        public readonly List<string> Params = [];
    }

    private sealed record Target(string Key, ShaderInstance Instance, ShaderInstance? OriginalShader, string? OriginalPrototype);

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<HumanoidAppearanceComponent> ent, ref ComponentShutdown args)
        => _masked.Remove(ent);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var humanoid, out var sprite))
        {
            var missing = GetMissing(humanoid);
            _masked.TryGetValue(uid, out var state);

            if (state != null && state.Missing == missing && IsIntact((uid, sprite), state))
                continue;

            if (state != null)
            {
                Clear((uid, sprite), state);
                _masked.Remove(uid);
            }

            if (missing != 0)
                _masked[uid] = Apply((uid, sprite), missing);
        }
    }

    private static int GetMissing(HumanoidAppearanceComponent humanoid)
    {
        if (humanoid.PermanentlyHidden.Count == 0)
            return 0;

        var missing = 0;
        for (var i = 0; i < _limbs.Length; i++)
        {
            if (humanoid.PermanentlyHidden.Contains(_limbs[i]))
                missing |= 1 << i;
        }

        return missing;
    }

    private bool IsIntact(Entity<SpriteComponent> ent, MaskState state)
    {
        if (ent.Comp.AllLayers.Count() != state.LayerCount)
            return false;

        foreach (var target in state.Targets)
        {
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), target.Key, out var index, false)
                || ent.Comp[index] is not SpriteComponent.Layer layer
                || layer.Shader != target.Instance)
                return false;
        }

        foreach (var param in state.Params)
        {
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), param, out _, false))
                return false;
        }

        return true;
    }

    private MaskState Apply(Entity<SpriteComponent> ent, int missing)
    {
        var state = new MaskState { Missing = missing };

        var masks = new List<(int Slot, SpriteSpecifier.Rsi Sprite)>();
        for (var i = 0; i < _limbs.Length; i++)
        {
            if ((missing & (1 << i)) != 0 && GetBaseSprite(ent, _limbs[i]) is { } limbSprite)
                masks.Add((i, limbSprite));
        }
        var keep = GetBaseSprite(ent, HumanoidVisualLayers.Chest);

        _baseLayers.Clear();
        foreach (var layerKey in Enum.GetValues<HumanoidVisualLayers>())
        {
            if (_sprite.LayerMapTryGet(ent.AsNullable(), layerKey, out var baseIndex, false))
                _baseLayers.Add(baseIndex);
        }

        var count = ent.Comp.AllLayers.Count();
        for (var i = 0; i < count; i++)
        {
            if (_baseLayers.Contains(i)
                || ent.Comp[i] is not SpriteComponent.Layer layer
                || layer.CopyToShaderParameters != null
                || GetMaskShader(layer) is not { } shader)
                continue;

            var key = TargetKeyPrefix + state.Targets.Count;
            var instance = _prototypes.Index(shader).InstanceUnique();
            for (var slot = 0; slot < _limbs.Length; slot++)
            {
                instance.SetParameter($"limbMask{slot}", Texture.Transparent);
                instance.SetParameter($"limbMaskUV{slot}", new Vector4(0, 0, 1, 1));
            }
            instance.SetParameter("keepMask", Texture.Transparent);
            instance.SetParameter("keepMaskUV", new Vector4(0, 0, 1, 1));

            state.Targets.Add(new Target(key, instance, layer.Shader, layer.ShaderPrototype?.Id));
            _sprite.LayerMapSet(ent.AsNullable(), key, i);
            ent.Comp.LayerSetShader(i, instance, shader);
        }

        foreach (var target in state.Targets)
        {
            foreach (var (slot, limbSprite) in masks)
                AddParam(ent, state, target.Key, limbSprite, $"limbMask{slot}", $"limbMaskUV{slot}");
            if (keep != null)
                AddParam(ent, state, target.Key, keep, "keepMask", "keepMaskUV");
        }

        state.LayerCount = ent.Comp.AllLayers.Count();
        return state;
    }

    private void AddParam(Entity<SpriteComponent> ent, MaskState state, string targetKey, SpriteSpecifier.Rsi sprite, string texture, string uv)
    {
        var index = _sprite.AddLayer(ent.AsNullable(), sprite, 0);
        if (ent.Comp[index] is not SpriteComponent.Layer layer)
            return;

        layer.CopyToShaderParameters = new SpriteComponent.CopyToShaderParameters(targetKey)
        {
            ParameterTexture = texture,
            ParameterUV = uv,
        };

        var key = ParamKeyPrefix + state.Params.Count;
        _sprite.LayerMapSet(ent.AsNullable(), key, index);
        state.Params.Add(key);
    }

    private void Clear(Entity<SpriteComponent> ent, MaskState state)
    {
        foreach (var param in state.Params)
            _sprite.RemoveLayer(ent.AsNullable(), param, false);

        foreach (var target in state.Targets)
        {
            if (!_sprite.LayerMapTryGet(ent.AsNullable(), target.Key, out var index, false))
                continue;

            if (ent.Comp[index] is SpriteComponent.Layer layer && layer.Shader == target.Instance)
                ent.Comp.LayerSetShader(index, target.OriginalShader, target.OriginalPrototype);
            _sprite.LayerMapRemove(ent.AsNullable(), target.Key);
        }
    }

    private SpriteSpecifier.Rsi? GetBaseSprite(Entity<SpriteComponent> ent, HumanoidVisualLayers layerKey)
        => !_sprite.LayerMapTryGet(ent.AsNullable(), layerKey, out var index, false)
        || ent.Comp[index] is not SpriteComponent.Layer layer
        || layer.ActualRsi is not { } rsi
        || layer.State.Name is not { } state
        ? null
        : new SpriteSpecifier.Rsi(rsi.Path, state);

    private static ProtoId<ShaderPrototype>? GetMaskShader(SpriteComponent.Layer layer)
        => layer.ShaderPrototype?.Id switch
        {
            null when layer.Shader == null => _maskShader,
            DisplacedDrawShader => _maskDisplacedShader,
            DisplacedStencilDrawShader => _maskDisplacedStencilShader,
            _ => (ProtoId<ShaderPrototype>?) null,
        };
}
