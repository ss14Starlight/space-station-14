using Content.Server.Pinpointer;
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed partial class OreSiloSystem : SharedOreSiloSystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private IGameTiming _timing = default!; // Starlight

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen

    private readonly HashSet<Entity<OreSiloClientComponent>> _clientLookup = new();
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    private readonly HashSet<EntityUid> _silosToAdd = new();
    private readonly HashSet<EntityUid> _silosToRemove = new();

    // Starlight-start: preload runs a few times a second over clients grouped by grid, instead of players x clients every tick.
    private static readonly TimeSpan PreloadInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextPreload;
    private readonly Dictionary<EntityUid, List<(System.Numerics.Vector2 Position, EntityUid Silo)>> _clientsByGrid = new();
    private readonly List<List<(System.Numerics.Vector2 Position, EntityUid Silo)>> _clientListPool = new();
    // Starlight-end

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientLookup.Clear();
        _clientInformation.Clear();

        var xform = Transform(ent);

        // Sneakily uses override with TComponent parameter
        _entityLookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.Range, _clientLookup);

        foreach (var client in _clientLookup)
        {
            // don't show already-linked clients.
            if (client.Comp.Silo is not null)
                continue;

            // Don't show clients on the screen if we can't link them.
            if (!CanTransmitMaterials((ent, ent, xform), client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client.Owner, onlyName: true);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", true));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        // Get all clients of this silo, including those out of range.
        foreach (var client in ent.Comp.Clients)
        {
            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        // Starlight-start
        var curTime = _timing.CurTime;
        if (curTime < _nextPreload)
            return;

        _nextPreload = curTime + PreloadInterval;

        foreach (var list in _clientsByGrid.Values)
        {
            list.Clear();
            _clientListPool.Add(list);
        }

        _clientsByGrid.Clear();

        var clientQuery = EntityQueryEnumerator<OreSiloClientComponent, TransformComponent>();
        while (clientQuery.MoveNext(out _, out var clientComp, out var clientXform))
        {
            if (clientComp.Silo == null || clientXform.GridUid is not { } clientGrid)
                continue;

            if (!_clientsByGrid.TryGetValue(clientGrid, out var gridClients))
            {
                if (_clientListPool.Count > 0)
                {
                    gridClients = _clientListPool[^1];
                    _clientListPool.RemoveAt(_clientListPool.Count - 1);
                }
                else
                {
                    gridClients = new();
                }

                _clientsByGrid[clientGrid] = gridClients;
            }

            gridClients.Add((clientXform.LocalPosition, clientComp.Silo.Value));
        }

        if (_clientsByGrid.Count == 0)
            return;
        // Starlight-end

        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out var actorComp, out var actorXform))
        {
            _silosToAdd.Clear();
            _silosToRemove.Clear();

            // Starlight-start: only clients on the actor's grid, collected above.
            // We limit it to same-grid checks only for peak perf
            if (actorXform.GridUid is not { } actorGrid || !_clientsByGrid.TryGetValue(actorGrid, out var gridClients))
                continue;

            foreach (var (clientPosition, silo) in gridClients)
            {
                if ((actorXform.LocalPosition - clientPosition).LengthSquared() <= OreSiloPreloadRangeSquared)
                {
                    _silosToAdd.Add(silo);
                }
                else
                {
                    _silosToRemove.Add(silo);
                }
            }
            // Starlight-end

            foreach (var toRemove in _silosToRemove)
            {
                _pvsOverride.RemoveSessionOverride(toRemove, actorComp.PlayerSession);
            }
            foreach (var toAdd in _silosToAdd)
            {
                _pvsOverride.AddSessionOverride(toAdd, actorComp.PlayerSession);
            }
        }
    }
}
