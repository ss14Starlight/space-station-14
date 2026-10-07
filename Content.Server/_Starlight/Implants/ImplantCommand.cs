using System.Linq;
using Content.Server._Starlight.Toolshed;
using Content.Server.Administration;
using Content.Server.Implants;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Content.Shared.Implants.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Implants;

/// <remarks>
/// Note: this refers to subdermal implants, not surgery implants.
/// </remarks>
[ToolshedCommand]
[AdminCommand(AdminFlags.Fun)]
public sealed class ImplantCommand : ToolshedCommand
{
    private SubdermalImplantSystem? _implant;

    /// <summary>
    /// Give the piped entity an implant.
    /// </summary>
    [CommandImplementation("give")]
    public EntityUid GiveImplant(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<SubdermalImplantComponent>))]
        EntProtoId implantProto)
    {
        _implant ??= GetSys<SubdermalImplantSystem>();
        if (_implant.TryGetImplants(uid, out var implants))
            if (implants.Any(implant => MetaData(implant).EntityPrototype?.ID == implantProto.Id))
            {
                CommandMarkup.Error(ctx,
                    $"Implant of type {implantProto} already exists on entity {EntityManager.ToPrettyString(uid)}.");
                return uid;
            }
        _implant.AddImplant(uid, implantProto);
        return uid;
    }

    /// <summary>
    /// Remove an implant from the piped entity, assuming they have one.
    /// </summary>
    [CommandImplementation("remove")]
    public EntityUid RemoveImplant(IInvocationContext ctx, [PipedArgument] EntityUid uid,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<SubdermalImplantComponent>))]
        EntProtoId implantProto)
    {
        _implant ??= GetSys<SubdermalImplantSystem>();
        if (_implant.TryGetImplants(uid, out var implants))
        {
            foreach (var implant in implants.Where(implant => MetaData(implant).EntityPrototype?.ID == implantProto.Id))
            {
                _implant.ForceRemove(uid, implant);
                return uid;
            }
            CommandMarkup.Error(ctx,
                $"Implant of type {implantProto} does not exist on entity {EntityManager.ToPrettyString(uid)}.");
            return uid;
        }
        CommandMarkup.Error(ctx, $"Entity {EntityManager.ToPrettyString(uid)} has no implants.");
        return uid;
    }

    [CommandImplementation("with")]
    public IEnumerable<EntityUid> WithRole([PipedArgument] IEnumerable<EntityUid> uids,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<SubdermalImplantComponent>))]
        EntProtoId implantProto,
        [CommandInverted] bool inverted)
    {
        _implant ??= GetSys<SubdermalImplantSystem>();
        List<EntityUid> results = [];
        foreach (var uid in uids)
        {
            if (!_implant.TryGetImplants(uid, out var implants))
            {
                if (inverted) results.Add(uid);
                continue;
            }

            if (implants.Any(implant => MetaData(implant).EntityPrototype?.ID == implantProto.Id) ^ inverted)
                results.Add(uid);
        }
        return results;
    }

    [CommandImplementation("has")]
    public bool HasRole([PipedArgument] EntityUid uid,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<SubdermalImplantComponent>))] EntProtoId implantProto,
        [CommandInverted] bool inverted)
    {
        _implant ??= GetSys<SubdermalImplantSystem>();
        if (!_implant.TryGetImplants(uid, out var implants)) return inverted;
        return implants.Any(implant => MetaData(implant).EntityPrototype?.ID == implantProto.Id) ^ inverted;
    }

    [CommandImplementation("give")]
    public IEnumerable<EntityUid> GiveImplant(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<SubdermalImplantComponent>))]
        EntProtoId implantProto) => uid.Select(x => GiveImplant(ctx, x, implantProto));

    [CommandImplementation("remove")]
    public IEnumerable<EntityUid> RemoveImplant(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> uid,
        [CommandArgument(typeof(EntProtoIdWithCompCompletionParser<SubdermalImplantComponent>))]
        EntProtoId implantProto) => uid.Select(x => RemoveImplant(ctx, x, implantProto));
}
