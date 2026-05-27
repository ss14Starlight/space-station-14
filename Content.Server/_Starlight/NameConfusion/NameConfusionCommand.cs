using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.NameConfusion;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.NameConfusion;

[ToolshedCommand(Name = "nconf")]
[AdminCommand(AdminFlags.Fun)]
public sealed class NameConfusionCommand : ToolshedCommand
{
    private NameConfusionSystem? _conf;

    /// <summary>
    /// Do a confusion. Can be forced to ignore probability checks.
    /// </summary>
    [CommandImplementation("confuse")]
    public EntityUid Confuse(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool forced)
    {
        if(!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.ConfuseName(uid, comp, forced);
        ctx.WriteLine($"Forced a name confusion on entity {uid}.");
        return uid;
    }

    /// <summary>
    /// Restore name. Confuse CAN do this but this guarantees it.
    /// </summary>
    [CommandImplementation("restore")]
    public EntityUid Restore(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        if (comp.CurrentName is null)
        {
            ctx.WriteLine($"Entity {uid}'s name was not confused.");
            return uid;
        }
        _conf?.RestoreName(uid, comp);
        ctx.WriteLine($"Restored entity {uid}'s name.");
        return uid;
    }

    /// <summary>
    /// Adds a name to the confusion name list. Adds the component if it doesn't exist.
    /// </summary>
    [CommandImplementation("addname")]
    public EntityUid AddName(IInvocationContext ctx, [PipedArgument] EntityUid uid, string name)
    {
        _conf ??= EntitySystemManager.GetEntitySystem<NameConfusionSystem>();
        var hadComp = HasComp<NameConfusionComponent>(uid);
        var comp = EnsureComp<NameConfusionComponent>(uid);
        var names = comp.Names;
        if (names.Contains(name))
        {
            ctx.WriteLine($"Entity {uid} already has the name {name} in the confusion name list.");
            return uid;
        }

        _conf?.AddConfusedName(uid, name, comp);
        if(!hadComp) ctx.WriteLine($"Added name confusion component to entity {uid}.");
        ctx.WriteLine($"Added name {name} to entity {uid}'s confusion name list.");
        return uid;
    }

    /// <summary>
    /// Removes a name from the confusion name list.
    /// </summary>
    [CommandImplementation("rmname")]
    public EntityUid RemoveName(IInvocationContext ctx, [PipedArgument] EntityUid uid, string name)
    {
        if(!EnsureWorkable(ctx, uid, out var comp)) return uid;
        var names = comp.Names;
        if (!names.Contains(name))
        {
            ctx.WriteLine($"The name {name} does not exist in entity {uid}'s confusion name list.");
            return uid;
        }

        _conf?.RemoveConfusedName(uid, name, comp);
        ctx.WriteLine($"Removed name {name} from entity {uid}'s confusion name list.");
        return uid;
    }

    /// <summary>
    /// Clears the confusion name list. Optionally, removes the component.
    /// </summary>
    [CommandImplementation("clearnames")]
    public EntityUid ClearNames(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool removeComponent)
    {
        if(!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.ClearConfusedNames(uid, comp);
        if(removeComponent) RemComp<NameConfusionComponent>(uid);
        ctx.WriteLine($"Cleared confusion names from entity {uid}{(removeComponent ? " and removed the component." : ".")}");
        return uid;
    }

    /// <summary>
    /// Sets <see cref="NameConfusionComponent.ConfuseOnSpeak"/>.
    /// </summary>
    [CommandImplementation("confuseonspeak")]
    public EntityUid SetConfuseOnSpeak(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool state)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.SetConfusedOnSpeak(uid, state, comp);
        ctx.WriteLine($"Set {nameof(comp.ConfuseOnSpeak)} on {uid} to {state}.");
        return uid;
    }

    /// <summary>
    /// Sets <see cref="NameConfusionComponent.ConfuseOnExamine"/>.
    /// </summary>
    [CommandImplementation("confuseonexamine")]
    public EntityUid SetConfuseOnExamine(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool state)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.SetConfusedOnExamine(uid, state, comp);
        ctx.WriteLine($"Set {nameof(comp.ConfuseOnExamine)} on {uid} to {state}.");
        return uid;
    }

    /// <summary>
    /// Sets <see cref="NameConfusionComponent.ConfuseOnInterval"/>.
    /// </summary>
    [CommandImplementation("confuseoninterval")]
    public EntityUid SetConfuseOnInterval(IInvocationContext ctx, [PipedArgument] EntityUid uid, bool state)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.SetConfusedOnInterval(uid, state, comp);
        ctx.WriteLine($"Set {nameof(comp.ConfuseOnInterval)} on {uid} to {state}.");
        return uid;
    }

    /// <summary>
    /// Sets <see cref="NameConfusionComponent.ConfuseInterval"/>.
    /// </summary>
    [CommandImplementation("confuseintervaltime")]
    public EntityUid SetConfuseIntervalTime(IInvocationContext ctx, [PipedArgument] EntityUid uid, float seconds)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        if (Math.Sign(seconds) < 0)
        {
            ctx.WriteMarkup("[color=red]Seconds cannot be negative.[/color]");
            return uid;
        }
        _conf?.SetConfusedIntervalTime(uid, TimeSpan.FromSeconds(seconds), comp);
        ctx.WriteLine($"Set {nameof(comp.ConfuseInterval)} on {uid} to {seconds} seconds.");
        return uid;
    }

    /// <summary>
    /// Sets <see cref="NameConfusionComponent.NameConfusionProbability"/>.
    /// </summary>
    [CommandImplementation("confuseprob")]
    public EntityUid SetConfusionProbability(IInvocationContext ctx, [PipedArgument] EntityUid uid, float prob)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.SetConfusionProbability(uid, prob, comp);
        ctx.WriteLine($"Set {nameof(comp.NameConfusionProbability)} on {uid} to {prob}.");
        return uid;
    }

    /// <summary>
    /// Sets <see cref="NameConfusionComponent.NameRestoreProbability"/>.
    /// </summary>
    [CommandImplementation("restoreprob")]
    public EntityUid SetRestoreProbability(IInvocationContext ctx, [PipedArgument] EntityUid uid, float prob)
    {
        if (!EnsureWorkable(ctx, uid, out var comp)) return uid;
        _conf?.SetRestoreProbability(uid, prob, comp);
        ctx.WriteLine($"Set {nameof(comp.NameRestoreProbability)} on {uid} to {prob}.");
        return uid;
    }

    [CommandImplementation("confuse")]
    public IEnumerable<EntityUid> Confuse(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        bool forced) =>
        uid.Select(x => Confuse(ctx, x, forced));

    [CommandImplementation("restore")]
    public IEnumerable<EntityUid> Restore(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => Restore(ctx, x));

    [CommandImplementation("addname")]
    public IEnumerable<EntityUid> AddName(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        string name) =>
        uid.Select(x => AddName(ctx, x, name));

    [CommandImplementation("rmname")]
    public IEnumerable<EntityUid> RemoveName(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        string name) =>
        uid.Select(x => RemoveName(ctx, x, name));

    [CommandImplementation("clearnames")]
    public IEnumerable<EntityUid> ClearNames(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        bool removeComponent) =>
        uid.Select(x => ClearNames(ctx, x, removeComponent));

    [CommandImplementation("confuseonspeak")]
    public IEnumerable<EntityUid> SetConfuseOnSpeak(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        bool state) =>
        uid.Select(x => SetConfuseOnSpeak(ctx, x, state));

    [CommandImplementation("confuseonexamine")]
    public IEnumerable<EntityUid> SetConfuseOnExamine(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uid, bool state) =>
        uid.Select(x => SetConfuseOnExamine(ctx, x, state));

    [CommandImplementation("confuseoninterval")]
    public IEnumerable<EntityUid> SetConfuseOnInterval(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uid, bool state) =>
        uid.Select(x => SetConfuseOnInterval(ctx, x, state));

    [CommandImplementation("confuseintervaltime")]
    public IEnumerable<EntityUid> SetConfuseIntervalTime(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uid, float seconds) =>
        uid.Select(x => SetConfuseIntervalTime(ctx, x, seconds));

    [CommandImplementation("confuseprob")]
    public IEnumerable<EntityUid> SetConfusionProbability(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uid, float prob) =>
        uid.Select(x => SetConfusionProbability(ctx, x, prob));

    [CommandImplementation("restoreprob")]
    public IEnumerable<EntityUid> SetRestoreProbability(IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> uid, float prob) =>
        uid.Select(x => SetRestoreProbability(ctx, x, prob));


    private bool EnsureWorkable(IInvocationContext ctx, EntityUid uid, [NotNullWhen(true)] out NameConfusionComponent? comp)
    {
        comp = null;
        _conf = EntitySystemManager.GetEntitySystem<NameConfusionSystem>();
        if (!TryComp(uid, out comp))
        {
            ctx.WriteMarkup($"[color=red]Entity {uid} has no {nameof(NameConfusionComponent)}. Run [color=yellow]nconf:addname[/color] first.[/color]");
            return false;
        }

        if (comp.Names.Count != 0) return true;
        ctx.WriteMarkup($"[color=red]Entity {uid} has no names to pick from. Run [color=yellow]nconf:addname[/color] first.[/color]");
        return false;
    }
}
