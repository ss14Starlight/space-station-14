using Content.Client.Light;
using System.Linq;
using Content.Shared._Starlight.Sprite;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Reflection;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Starlight.Sprite;

public sealed partial class ColorControlSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = null!;
    [Dependency] private RgbLightControllerSystem _rgb = null!;
    [Dependency] private SpriteLayerGroupsSystem _groups = null!;
    [Dependency] private IReflectionManager _reflection = null!;

    private const string Unshaded = "unshaded";

    /// <summary>
    /// Used to try and prevent clothing from dying when making unshaded.
    /// </summary>
    private static readonly Dictionary<string, (string Shaded, string Unshaded)> _displacementShaders = new()
    {
        ["DisplacedDraw"] = ("DisplacedDraw", "DisplacedDrawUnshaded"),
        ["DisplacedDrawUnshaded"] = ("DisplacedDraw", "DisplacedDrawUnshaded"),
    };

    private readonly HashSet<int> _clothingIndices = [];
    private readonly HashSet<int> _heldIndices = [];
    private readonly HashSet<int> _manualIndices = [];
    private readonly Dictionary<int, ColorControlLayerOverride> _overrideIndices = [];

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<ColorControlComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp<SpriteComponent>(ent, out var sprite))
            for (var i = 0; i < sprite.LayerData.Count; i++)
            {
                var layer = sprite.LayerData[i];
                if (ent.Comp.TrackedLayers.TryGetValue(layer, out var tracked))
                    Restore(ent, sprite, i, layer, tracked);
            }

        ent.Comp.TrackedLayers.Clear();
        ent.Comp.IndexedLayers.Clear();
        ent.Comp.ResolvedIndices = null;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<ColorControlComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite)) UpdateEntity(uid, comp, sprite);
    }

    private void UpdateEntity(EntityUid uid, ColorControlComponent comp, SpriteComponent sprite)
    {
        var frame = ++comp.Frame;

        // Indices are only meaningful at the moment they are set, so resolve them to layers once per change.
        if (comp.ResolvedIndices == null || !comp.ResolvedIndices.SequenceEqual(comp.LayerIndices))
        {
            // ReSharper disable once UseCollectionExpression | Sandboxing skill issue
            comp.ResolvedIndices = new List<int>(comp.LayerIndices);
            comp.IndexedLayers.Clear();
            foreach (var index in comp.LayerIndices.Where(index => index >= 0 && index < sprite.LayerData.Count))
                comp.IndexedLayers.Add(sprite.LayerData[index]);
        }

        var manual = comp.Layers.Count > 0 || comp.LayerIndices.Count > 0;
        if (manual)
            CollectManual(uid, sprite, comp);
        else
            _groups.Collect(uid, sprite, _clothingIndices, _heldIndices);

        _overrideIndices.Clear();
        foreach (var (key, layerOverride) in comp.LayerOverrides)
            if (TryGetLayerIndex(uid, sprite, key, out var index))
                _overrideIndices[index] = layerOverride;

        for (var i = 0; i < sprite.LayerData.Count; i++)
        {
            var layer = sprite.LayerData[i];
            _overrideIndices.TryGetValue(i, out var layerOverride);

            var included = manual
                ? _manualIndices.Contains(i) || comp.IndexedLayers.Contains(layer)
                : IsInGroups(i, comp.Targets);

            if (included && (layer.CopyToShaderParameters != null || IsIgnored(comp, layer)))
                included = false;

            if (!included && layerOverride == null)
            {
                if (comp.TrackedLayers.Remove(layer, out var stale))
                    Restore(uid, sprite, i, layer, stale);
                continue;
            }

            if (!comp.TrackedLayers.TryGetValue(layer, out var tracked))
            {
                tracked = new ColorControlTrackedLayer
                {
                    OriginalColor =
                        _rgb.TryGetOriginalColor(uid, layer, i, out var rgbOriginal) ? rgbOriginal : layer.Color,
                    OriginalShaderProto = layer.ShaderPrototype?.Id,
                    OriginalShader = layer.Shader,
                };
                comp.TrackedLayers[layer] = tracked;
            }

            tracked.LastSeen = frame;
            var color = layerOverride?.Color ?? (included ? comp.Color : null);
            var mode = layerOverride?.Mode ?? comp.Mode;
            var shader = layerOverride?.Shader ?? (included ? comp.Shader : null);

            ApplyColor(uid, i, color, mode, layer, tracked);
            ApplyShader(shader, sprite, i, layer, tracked);
        }

        if (comp.TrackedLayers.Count > 0)
            foreach (var (layer, tracked) in comp.TrackedLayers)
                if (tracked.LastSeen != frame)
                    comp.TrackedLayers.Remove(layer);

        if (comp.IndexedLayers.Count > 0)
            comp.IndexedLayers.RemoveWhere(l => !sprite.LayerData.Contains((Layer)l));
    }

    private void CollectManual(EntityUid uid, SpriteComponent sprite, ColorControlComponent comp)
    {
        _manualIndices.Clear();
        foreach (var key in comp.Layers)
            if (TryGetLayerIndex(uid, sprite, key, out var index))
                _manualIndices.Add(index);
    }

    private bool IsInGroups(int index, ColorControlTargets targets)
    {
        if (_clothingIndices.Contains(index))
            return (targets & ColorControlTargets.Clothing) != 0;

        if (_heldIndices.Contains(index))
            return (targets & ColorControlTargets.Held) != 0;

        return (targets & ColorControlTargets.Body) != 0;
    }

    private bool TryGetLayerIndex(EntityUid uid, SpriteComponent sprite, string key, out int index)
    {
        if (key.StartsWith("enum.") && _reflection.TryParseEnumReference(key, out var @enum, false))
            return _sprite.LayerMapTryGet((uid, sprite), @enum, out index, false);

        return _sprite.LayerMapTryGet((uid, sprite), key, out index, false);
    }

    private static bool IsIgnored(ColorControlComponent comp, Layer layer)
    {
        var shader = comp.TrackedLayers.TryGetValue(layer, out var tracked) && tracked.ShaderApplied
            ? tracked.OriginalShaderProto
            : layer.ShaderPrototype?.Id;

        return shader != null && comp.IgnoredShaders.Contains(shader);
    }

    private void ApplyColor(EntityUid uid, int index, Color? newColor, ColorControlMode mode, Layer layer,
        ColorControlTrackedLayer tracked)
    {
        if (newColor is not { } color)
        {
            if (!tracked.ColorApplied) return;
            RestoreColor(uid, layer, tracked);
            tracked.ColorApplied = false;

            return;
        }

        if (!tracked.ColorApplied)
            tracked.OriginalColor = _rgb.TryGetOriginalColor(uid, layer, index, out var rgbOriginal)
                ? rgbOriginal
                : layer.Color;
        else if (layer.Color != tracked.AppliedColor && !_rgb.IsModulating(uid, layer, index))
            tracked.OriginalColor = layer.Color;

        var target = mode == ColorControlMode.Multiply
            ? tracked.OriginalColor * color
            : color;

        if (layer.Color != target)
            _sprite.LayerSetColor(layer, target);

        tracked.AppliedColor = target;
        tracked.ColorApplied = true;
    }

    private static void ApplyShader(string? newShader, SpriteComponent sprite, int index, Layer layer,
        ColorControlTrackedLayer tracked)
    {
        if (newShader is null)
        {
            if (!tracked.ShaderApplied) return;
            RestoreShader(sprite, index, tracked);
            tracked.ShaderApplied = false;

            return;
        }

        if (!tracked.ShaderApplied || layer.ShaderPrototype?.Id != tracked.AppliedShader)
        {
            tracked.OriginalShaderProto = layer.ShaderPrototype?.Id;
            tracked.OriginalShader = layer.Shader;
        }

        var target = newShader;
        if (tracked.OriginalShaderProto is { } original &&
            _displacementShaders.TryGetValue(original, out var variants))
            target = newShader == Unshaded ? variants.Unshaded : variants.Shaded;

        if (layer.ShaderPrototype?.Id != target)
            sprite.LayerSetShader(index, target);

        tracked.AppliedShader = target;
        tracked.ShaderApplied = true;
    }

    private void Restore(EntityUid uid, SpriteComponent sprite, int index, Layer layer,
        ColorControlTrackedLayer tracked)
    {
        if (tracked.ColorApplied)
            RestoreColor(uid, layer, tracked);

        if (tracked.ShaderApplied)
            RestoreShader(sprite, index, tracked);
    }

    private void RestoreColor(EntityUid uid, Layer layer, ColorControlTrackedLayer tracked)
    {
        _sprite.LayerSetColor(layer, tracked.OriginalColor);
        _rgb.ForgetAutoLayer(uid, layer);
    }

    private static void RestoreShader(SpriteComponent sprite, int index, ColorControlTrackedLayer tracked)
    {
        if (tracked.OriginalShaderProto == Unshaded)
            sprite.LayerSetShader(index, Unshaded);
        else
            sprite.LayerSetShader(index, (ShaderInstance?)tracked.OriginalShader, tracked.OriginalShaderProto);
    }
}
