using Content.Shared.Body.Systems;
using Content.Shared._Starlight.Body.Systems;
using Content.Shared._Starlight.Body.Prototypes;
using Content.Shared._Starlight.Body.Components;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Server.Administration;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Starlight.Administration.Systems.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class AddBodyPartCommand : LocalizedEntityCommands
{
    [Dependency] private BodySystem _bodySystem = default!;

    public override string Command => "addbodypart";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 4)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var childNetId) || !EntityManager.TryGetEntity(childNetId, out var childId))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid", ("uid", args[0])));
            return;
        }

        if (!NetEntity.TryParse(args[1], out var parentNetId) || !EntityManager.TryGetEntity(parentNetId, out var parentId))
        {
            shell.WriteError(Loc.GetString("shell-invalid-entity-uid", ("uid", args[1])));
            return;
        }

        var slBody = EntityManager.System<SLBodySystem>();
        var socketId = SharedBodySystem.GetSocketIdForLegacySlot(args[2]);
        if (Enum.TryParse<BodyPartType>(args[3], out _)
            && EntityManager.TryGetComponent<SLBodyPartComponent>(parentId.Value, out var parentPart)
            && EntityManager.TryGetComponent<SLBodyPartComponent>(childId.Value, out var childPart)
            && EnsureSocket(slBody, (parentId.Value, parentPart), socketId)
            && slBody.TryAttachPart((parentId.Value, parentPart), socketId, (childId.Value, childPart)))
        {
            shell.WriteLine($@"Added {childId} to {parentId}.");
        }
        else
            shell.WriteError($@"Could not add {childId} to {parentId}.");
    }

    private static bool EnsureSocket(SLBodySystem slBody, Entity<SLBodyPartComponent> part, string socketId)
    {
        if (!part.Comp.Sockets.ContainsKey(socketId))
            slBody.AddSocket(part, socketId, BodyPrefabPrototype.DefaultExternalSocket);
        return true;
    }
}
