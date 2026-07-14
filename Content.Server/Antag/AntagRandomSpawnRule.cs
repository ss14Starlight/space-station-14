using Content.Server.Antag.Components;
using Content.Server._Starlight.StationEvents.Components; // Starlight
using Content.Server._Starlight.StationEvents.Events; // Starlight
using Content.Shared.GameTicking.Components;
using Content.Server.GameTicking.Rules;

namespace Content.Server.Antag;

public sealed partial class AntagRandomSpawnSystem : GameRuleSystem<AntagRandomSpawnComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagRandomSpawnComponent, AntagSelectLocationEvent>(OnSelectLocation,
            after: [typeof(VentSpawnRule)]); // Starlight, vent spawn rule fallback
    }

    protected override void Added(EntityUid uid, AntagRandomSpawnComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        #region Starlight
        // we have to select this here because AntagSelectLocationEvent is raised twice because MakeAntag is called twice
        // once when a ghost role spawner is created and once when someone takes the ghost role

        //if (TryFindRandomTile(out _, out _, out _, out var coords))
        //    comp.Coords = coords;

        TryRefreshCoords(comp);
        #endregion
    }

    private void OnSelectLocation(Entity<AntagRandomSpawnComponent> ent, ref AntagSelectLocationEvent args)
    {
        #region Starlight
        // Only enforce strict vent-first fallback when this rule actually has vent spawning.
        if (HasComp<VentSpawnRuleComponent>(ent.Owner) && args.Coordinates.Count > 0)
            return;

        if (ent.Comp.Coords == null)
            TryRefreshCoords(ent.Comp);
        #endregion

        if (ent.Comp.Coords != null)
            args.Coordinates.Add(ent.Comp.Coords.Value); // Starlight, add the selected coordinates
    }

    #region Starlight
    /// <summary>
    /// Attempts to refresh the coordinates for the given component, first trying to select a random station tile, and falling back to the observer spawn point if necessary.
    /// </summary>
    /// <param name="comp"></param>
    private void TryRefreshCoords(AntagRandomSpawnComponent comp)
    {
        // First preference: station tile selection.
        if (TryFindRandomTile(out _, out _, out _, out var coords))
        {
            if (_transform.IsValid(coords))
                comp.Coords = _transform.ToMapCoordinates(coords, logError: false);

            return;
        }

        // Fallback so antag ghost-role spawner placement never fails hard in edge-case maps/tests.
        var observerCoords = GameTicker.GetObserverSpawnPoint();
        if (_transform.IsValid(observerCoords))
            comp.Coords = _transform.ToMapCoordinates(observerCoords, logError: false);
    }
    #endregion
}
