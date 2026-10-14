using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Components;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class AddMechanismCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "addmechanism";
    public string Description => "Adds a given entity to a containing body.";
    public string Help => "Usage: addmechanism <entity uid> <bodypart uid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var organIdNet) || !_entManager.TryGetEntity(organIdNet, out var organId))
        {
            shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var partIdNet) || !_entManager.TryGetEntity(partIdNet, out var partId))
        {
            shell.WriteError(Loc.GetString("shell-entity-uid-must-be-number"));
            return;
        }

        if (TryAddToFreeOrganSocket(partId.Value, organId.Value))
        {
            shell.WriteLine($@"Added {organId} to {partId}.");
        }
        else
        {
            shell.WriteError($@"Could not add {organId} to {partId}.");
        }
    }

    private bool TryAddToFreeOrganSocket(EntityUid partId, EntityUid organId)
    {
        if (!_entManager.TryGetComponent<SLBodyPartComponent>(partId, out var part)
            || !_entManager.TryGetComponent<SLBodyPartComponent>(organId, out var organ))
            return false;

        var slBody = _entManager.System<SLBodySystem>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        foreach (var (socketId, socketType) in part.Sockets)
        {
            if (protoMan.Index(socketType).Internal && slBody.TryAttachPart((partId, part), socketId, (organId, organ)))
                return true;
        }

        var extra = 0;
        while (part.Sockets.ContainsKey($"Extra{extra}"))
            extra++;
        slBody.AddSocket((partId, part), $"Extra{extra}", BodyPrefabPrototype.DefaultInternalSocket);
        return slBody.TryAttachPart((partId, part), $"Extra{extra}", (organId, organ));
    }
}
