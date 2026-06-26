using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.EntitySystems;
using Content.Shared.Administration;
using Robust.Shared.Map.Components;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Atmos;

[ToolshedCommand]
[AdminCommand(AdminFlags.Debug)]
public sealed class AtmosCommand : ToolshedCommand
{
    private AtmosphereSystem? _atmos;
    private AtmosDeviceSystem? _device;

    [CommandImplementation("rejoin")]
    public EntityUid RejoinAtmosDevice(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _device ??= EntitySystemManager.GetEntitySystem<AtmosDeviceSystem>();

        if (!TryComp<AtmosDeviceComponent>(uid, out var device))
        {
            ctx.WriteMarkup($"[color=red]Entity {uid} either doesn't exist or is not an atmos device.[/color]");
            return uid;
        }

        _device.RejoinAtmosphere((uid, device));
        ctx.WriteLine($"Attempted to make atmos device with uid {uid} rejoin an atmosphere.");
        return uid;
    }

    [CommandImplementation("fix")]
    public EntityUid FixGridAtmos(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        _atmos ??= EntitySystemManager.GetEntitySystem<AtmosphereSystem>();

        if (!TryEnsureAtmosphere(ctx, uid, out var grid, out var atmos))
            return uid;

        _atmos.RebuildGridAtmosphere((uid, atmos, grid));
        ctx.WriteLine($"Fixed atmosphere of grid with uid {uid}");
        return uid;
    }

    [CommandImplementation("add")]
    public EntityUid AddGridAtmos(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (HasComp<GridAtmosphereComponent>(uid))
        {
            ctx.WriteLine($"Entity {uid} already has an atmosphere.");
            return uid;
        }
        if (!TryEnsureAtmosphere(ctx, uid, out _, out _))
            return uid;

        ctx.WriteLine($"Added atmosphere to grid with uid {uid}");
        return uid;
    }

    [CommandImplementation("rejoin")]
    public IEnumerable<EntityUid>
        RejoinAtmosDevice(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => RejoinAtmosDevice(ctx, x));

    [CommandImplementation("fix")]
    public IEnumerable<EntityUid> FixGridAtmos(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => FixGridAtmos(ctx, x));

    [CommandImplementation("add")]
    public IEnumerable<EntityUid> AddGridAtmos(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => AddGridAtmos(ctx, x));

    private bool TryEnsureAtmosphere(IInvocationContext ctx, EntityUid uid, [NotNullWhen(true)] out MapGridComponent? grid, [NotNullWhen(true)] out GridAtmosphereComponent? atmos)
    {
        atmos = null;
        if (!TryComp(uid, out grid))
        {
            ctx.WriteMarkup($"[color=red]Entity {uid} doesn't exist or is not a grid.[/color]");
            return false;
        }
        atmos = EnsureComp<GridAtmosphereComponent>(uid);
        return true;
    }
}
