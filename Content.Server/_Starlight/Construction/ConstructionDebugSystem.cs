using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Sandbox;
using Content.Shared.Administration;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Database;
using Content.Shared._Starlight.Construction;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Construction;

/// <summary>
/// Handles the debug option that instantly finishes every construction ghost a client has placed.
/// </summary>
public sealed partial class ConstructionDebugSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SandboxSystem _sandbox = default!;

    public override void Initialize()
    {
        SubscribeNetworkEvent<DebugFinishConstructionGhostsMessage>(OnFinishGhosts);
    }

    private void OnFinishGhosts(DebugFinishConstructionGhostsMessage ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (!_sandbox.IsSandboxEnabled && !_admin.HasAdminFlag(session, AdminFlags.Spawn))
            return;

        var user = session.AttachedEntity;
        var built = 0;

        foreach (var ghost in ev.Ghosts)
        {
            if (!_prototype.TryIndex(ghost.PrototypeName, out ConstructionPrototype? recipe)
                || !_prototype.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph)
                || recipe.TargetNode is not { } targetNodeId
                || !graph.Nodes.TryGetValue(targetNodeId, out var targetNode)
                || targetNode.Entity.GetId(null, user, new(EntityManager)) is not { } targetProtoId)
            {
                continue;
            }

            var coords = GetCoordinates(ghost.Location);

            if (!coords.IsValid(EntityManager))
                continue;

            var structure = SpawnAttachedTo(targetProtoId, coords, rotation: ghost.Angle);
            built++;

            RaiseNetworkEvent(new AckStructureConstructionMessage(ghost.Ack, GetNetEntity(structure)), session);
        }

        if (built == 0)
            return;

        _adminLogger.Add(LogType.Construction, LogImpact.High,
            $"{ToPrettyString(user):player} instantly finished {built} construction ghost(s).");
    }
}
