using Content.Shared.Alert;
using Content.Shared.Shuttles.BUIStates;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared._Starlight.Astronav;
public sealed partial class AstroNavMobSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AstroNavMobComponent, GPSAlertEvent>(OnGPSAlert);
    }
    private void OnGPSAlert(Entity<AstroNavMobComponent> ent, ref GPSAlertEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.User, out var actor))
            return;
        _uiSystem.TryToggleUi(args.User, RadarConsoleUiKey.Key, actor.PlayerSession);
        args.Handled = true;
    }
}
