using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.Sprite;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Sprite;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class ColorControlCommand : ToolshedCommand
{
    [CommandImplementation("color")]
    public EntityUid SetColor([PipedArgument] EntityUid uid, Color? color = null,
        ColorControlMode mode = ColorControlMode.Replace) => Modify(uid, comp =>
    {
        comp.Color = color;
        if (true) comp.Mode = mode;
    });

    [CommandImplementation("shader")]
    public EntityUid SetShader([PipedArgument] EntityUid uid, string? shader = null) =>
        Modify(uid, comp => comp.Shader = shader);

    [CommandImplementation("targets")]
    public EntityUid SetTargets([PipedArgument] EntityUid uid, ColorControlTargets[]? targets) => Modify(uid,
        comp =>
        {
            targets ??= [ColorControlTargets.Body];
            comp.Targets = targets.Aggregate(ColorControlTargets.None, (acc, t) => acc | t);
        });

    [CommandImplementation("addlayer")]
    public EntityUid AddLayer([PipedArgument] EntityUid uid, string? key = null, int? index = null) => Modify(uid,
        comp =>
        {
            if (key != null && !comp.Layers.Contains(key)) comp.Layers.Add(key);
            if (index is { } i && !comp.LayerIndices.Contains(i)) comp.LayerIndices.Add(i);
        });

    [CommandImplementation("removelayer")]
    public EntityUid RemoveLayer([PipedArgument] EntityUid uid, string? key = null, int? index = null) => Modify(uid,
        comp =>
        {
            if (key != null) comp.Layers.Remove(key);
            if (index is { } i) comp.LayerIndices.Remove(i);
        });

    [CommandImplementation("clearlayers")]
    public EntityUid ClearLayers([PipedArgument] EntityUid uid) => Modify(uid, comp =>
    {
        comp.Layers.Clear();
        comp.LayerIndices.Clear();
    });

    [CommandImplementation("overridelayer")]
    public EntityUid SetLayerOverride([PipedArgument] EntityUid uid, string key, Color? color = null,
        ColorControlMode mode = ColorControlMode.Replace, string? shader = null) => Modify(uid, comp =>
    {
        if (color == null && shader == null) comp.LayerOverrides.Remove(key);
        else comp.LayerOverrides[key] = new ColorControlLayerOverride { Color = color, Mode = mode, Shader = shader };
    });

    [CommandImplementation("clearoverrides")]
    public EntityUid ClearOverrides([PipedArgument] EntityUid uid) => Modify(uid, comp => comp.LayerOverrides.Clear());

    [CommandImplementation("ignoreshader")]
    public EntityUid IgnoreShader([PipedArgument] EntityUid uid, string shader, bool ignore = true) => Modify(uid,
        comp =>
        {
            if (ignore) comp.IgnoredShaders.Add(shader);
            else comp.IgnoredShaders.Remove(shader);
        });

    [CommandImplementation("reset")]
    public EntityUid Reset([PipedArgument] EntityUid uid)
    {
        RemComp<ColorControlComponent>(uid);
        return uid;
    }

    [CommandImplementation("color")]
    public IEnumerable<EntityUid> SetColor([PipedArgument] IEnumerable<EntityUid> uid, Color? color = null,
        ColorControlMode mode = ColorControlMode.Replace) => uid.Select(x => SetColor(x, color, mode));

    [CommandImplementation("shader")]
    public IEnumerable<EntityUid> SetShader([PipedArgument] IEnumerable<EntityUid> uid, string? shader = null) =>
        uid.Select(x => SetShader(x, shader));

    [CommandImplementation("targets")]
    public IEnumerable<EntityUid>
        SetTargets([PipedArgument] IEnumerable<EntityUid> uid, ColorControlTargets[]? targets) =>
        uid.Select(x => SetTargets(x, targets));

    [CommandImplementation("addlayer")]
    public IEnumerable<EntityUid> AddLayer([PipedArgument] IEnumerable<EntityUid> uid, string? key = null,
        int? index = null) => uid.Select(x => AddLayer(x, key, index));

    [CommandImplementation("removelayer")]
    public IEnumerable<EntityUid> RemoveLayer([PipedArgument] IEnumerable<EntityUid> uid, string? key = null,
        int? index = null) => uid.Select(x => RemoveLayer(x, key, index));

    [CommandImplementation("clearlayers")]
    public IEnumerable<EntityUid> ClearLayers([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(ClearLayers);

    [CommandImplementation("overridelayer")]
    public IEnumerable<EntityUid> SetLayerOverride([PipedArgument] IEnumerable<EntityUid> uid, string key,
        Color? color = null, ColorControlMode mode = ColorControlMode.Replace, string? shader = null) =>
        uid.Select(x => SetLayerOverride(x, key, color, mode, shader));

    [CommandImplementation("clearoverrides")]
    public IEnumerable<EntityUid> ClearOverrides([PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(ClearOverrides);

    [CommandImplementation("ignoreshader")]
    public IEnumerable<EntityUid> IgnoreShader([PipedArgument] IEnumerable<EntityUid> uid, string shader,
        bool ignore = true) => uid.Select(x => IgnoreShader(x, shader, ignore));

    [CommandImplementation("reset")]
    public IEnumerable<EntityUid> Reset([PipedArgument] IEnumerable<EntityUid> uid) => uid.Select(Reset);

    private EntityUid Modify(EntityUid uid, Action<ColorControlComponent> modify)
    {
        var comp = EnsureComp<ColorControlComponent>(uid);
        modify(comp);
        EntityManager.Dirty(uid, comp);
        return uid;
    }
}
