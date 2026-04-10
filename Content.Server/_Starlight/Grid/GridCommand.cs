using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Starlight.Grid;

[AdminCommand(AdminFlags.Admin)]
[ToolshedCommand]
public sealed class GridCommand : ToolshedCommand
{
    [Dependency] private readonly IPlayerManager _plr = default!;

    [CommandImplementation("getplayers")]
    public IEnumerable<EntityUid> GetPlayersOnGrid([PipedArgument] EntityUid grid, bool excludeGhosts = false)
    {
        var sessions = _plr.Sessions.Where(session =>
            session.AttachedEntity is not null && Transform(session.AttachedEntity.Value).GridUid == grid);
        List<EntityUid> entities = [];
        foreach (var session in sessions)
        {
            if (session.AttachedEntity is null) continue;
            entities.Add(session.AttachedEntity.Value);
        }
        if (excludeGhosts) entities.RemoveAll(HasComp<GhostComponent>);
        return entities;
    }

    [CommandImplementation("getplayers")]
    public IEnumerable<EntityUid> GetPlayersOnGrids([PipedArgument] IEnumerable<EntityUid> grids, bool excludeGhosts = false) =>
        grids.SelectMany(x => GetPlayersOnGrid(x));

    [CommandImplementation("get")]
    public EntityUid GetGrid([PipedArgument] EntityUid uid) => Transform(uid).GridUid ?? EntityUid.Invalid;

    [CommandImplementation("get")]
    public IEnumerable<EntityUid> GetGrids([PipedArgument] IEnumerable<EntityUid> uids) => uids.Select(GetGrid);

    [CommandImplementation("getstation")]
    public EntityUid GetStation([PipedArgument] EntityUid uid) => !TryComp<StationMemberComponent>(uid, out var member)
        ? EntityUid.Invalid
        : member.Station;

    [CommandImplementation("getstation")]
    public IEnumerable<EntityUid> GetStations([PipedArgument] IEnumerable<EntityUid> uids) => uids.Select(GetStation);
}
