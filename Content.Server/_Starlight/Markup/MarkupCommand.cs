using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration;
using Content.Shared._Starlight.Markup.Components;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Markup;

[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class MarkupCommand : ToolshedCommand
{
    private MarkupTextSystem? _markup;

    [CommandImplementation("adddesc")]
    public EntityUid AddDescription(IInvocationContext ctx, [PipedArgument] EntityUid uid, string id, string text)
    {
        EnsureDescriptionComp(uid, out var comp);
        if (comp.Texts.Any(kvp => kvp.Key == id))
        {
            ctx.WriteLine($"A description text with the id {id} already exists on the entity {uid}.");
            return uid;
        }
        _markup?.AddDescriptionText((uid, comp), id, text);
        ctx.WriteLine($"Added text with id {id} to {uid}'s description.");
        return uid;
    }

    [CommandImplementation("editdesc")]
    public EntityUid EditDescription(IInvocationContext ctx, [PipedArgument] EntityUid uid, string id, string text)
    {
        EnsureDescriptionComp(uid, out var comp);
        if (comp.Texts.All(kvp => kvp.Key != id))
        {
            ctx.WriteLine($"No description text with the id {id} exists on the entity {uid}.");
            return uid;
        }
        _markup?.EditDescriptionText((uid, comp), id, text);
        ctx.WriteLine($"Updated text with id {id} in {uid}'s description.");
        return uid;
    }

    [CommandImplementation("rmdesc")]
    public EntityUid RemoveDescription(IInvocationContext ctx, [PipedArgument] EntityUid uid, string id)
    {
        EnsureDescriptionComp(uid, out var comp);
        if (comp.Texts.All(kvp => kvp.Key != id))
        {
            ctx.WriteLine($"No description text with the id {id} exists on the entity {uid}.");
            return uid;
        }
        _markup?.RemoveDescriptionText((uid, comp), id);
        ctx.WriteLine($"Removed text with id {id} from {uid}'s description.");
        return uid;
    }

    [CommandImplementation("cleardesc")]
    public EntityUid ClearDescription(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        EnsureDescriptionComp(uid, out var comp);
        _markup?.ClearDescriptionText((uid, comp));
        ctx.WriteLine($"Cleared all additional markup text from {uid}'s description.");
        return uid;
    }

    [CommandImplementation("listdesc")]
    public EntityUid ListDescriptions(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        EnsureDescriptionComp(uid, out var comp);
        if (comp.Texts.Count == 0)
        {
            ctx.WriteLine($"Entity with uid {uid} has no markup descriptions.");
            return uid;
        }
        ctx.WriteLine($"Markup descriptions for entity {uid}:");
        foreach (var kvp in comp.Texts)
            ctx.WriteLine($"- {kvp.Key}: \"{kvp.Value.Replace("\"", "\\\"")}\"");
        return uid;
    }

    [CommandImplementation("adddesc")]
    public IEnumerable<EntityUid> AddDescription(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        string id, string text) =>
        uid.Select(x => AddDescription(ctx, x, id, text));

    [CommandImplementation("editdesc")]
    public IEnumerable<EntityUid> EditDescription(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        string id, string text) =>
        uid.Select(x => EditDescription(ctx, x, id, text));

    [CommandImplementation("rmdesc")]
    public IEnumerable<EntityUid> RemoveDescription(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        string id) =>
        uid.Select(x => RemoveDescription(ctx, x, id));

    [CommandImplementation("cleardesc")]
    public IEnumerable<EntityUid>
        ClearDescription(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => ClearDescription(ctx, x));

    [CommandImplementation("listdesc")]
    public IEnumerable<EntityUid>
        ListDescriptions(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid) =>
        uid.Select(x => ListDescriptions(ctx, x));

    private void EnsureDescriptionComp(EntityUid uid, out MarkupDescriptionComponent comp)
    {
        _markup ??= EntitySystemManager.GetEntitySystem<MarkupTextSystem>();
        comp = EnsureComp<MarkupDescriptionComponent>(uid);
    }
}
