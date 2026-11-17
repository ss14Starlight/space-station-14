using System.Linq;
using System.Reflection;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

// ReSharper disable CheckNamespace
namespace Content.Server.Administration.Managers;

public sealed partial class AdminManager
{
    public event Action<int>? OnActiveAdminsCountChanged;
    public event Action<int>? OnAdminsCountChanged;

    private (bool isAvail, AdminFlags[] flagsReq)? GetToolshedImplementationFlags(CommandSpec spec)
    {
        var methods = spec.Cmd.GetType()
            .GetMethods(ToolshedCommand.MethodFlags)
            .Where(m => m.GetCustomAttribute<CommandImplementationAttribute>() is { } attr
                && attr.SubCommand == spec.SubCommand);

        var any = false;
        var flags = new List<AdminFlags>();
        foreach (var method in methods)
        {
            any |= method.IsDefined(typeof(AnyCommandAttribute));
            flags.AddRange(method.GetCustomAttributes<AdminCommandAttribute>().Select(a => a.Flags));
        }

        if (flags.Count == 0) return any ? (true, Array.Empty<AdminFlags>()) : null;
        if (any)
            _sawmill.Error($"Toolshed command {spec.FullName()} has implementations with both [AnyCommand] and [AdminCommand]; using [AdminCommand].");

        return (true, flags.Distinct().ToArray());
    }
}
