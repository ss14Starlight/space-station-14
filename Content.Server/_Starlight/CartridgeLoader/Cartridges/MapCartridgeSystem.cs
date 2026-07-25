using Content.Server.CartridgeLoader;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.Station.Systems;
using Content.Shared._Starlight.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.Pinpointer;

namespace Content.Server._Starlight.CartridgeLoader.Cartridges;

/// <summary>
/// This handles...
/// </summary>
public sealed class MapCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private StationSystem _stationSystem = default!;


    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<MapCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        SubscribeLocalEvent<MapCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    private void UpdateUiState(EntityUid owner, EntityUid loader)
    {
        var stationId = _stationSystem.GetOwningStation(owner);
        if (stationId != null)
        {
            var loaderNet = GetNetEntity(loader);
            var stationNet = GetNetEntity(stationId.Value);
            var state = new MapUiState(stationNet, loaderNet);
            _cartridgeLoaderSystem.UpdateCartridgeUiState(loader, state);
        }
    }

    private void OnUiReady(Entity<MapCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader);
    }

    private void OnCartridgeRemoved(Entity<MapCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        if (!_cartridgeLoaderSystem.HasProgram<MapCartridgeComponent>(args.Loader))
        {
            RemComp<StationMapComponent>(args.Loader);
        }
    }

    private void OnCartridgeAdded(Entity<MapCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        EnsureComp<StationMapComponent>(args.Loader);
    }

}
