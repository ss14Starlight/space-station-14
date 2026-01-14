using Content.Shared._Starlight.Holograms;
using Content.Shared.Power;

namespace Content.Server._Starlight.Holograms;

public sealed partial class HologramServerSystem : EntitySystem
{
    [Dependency] private readonly HologramSystem _hologram = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramServerComponent, PowerChangedEvent>(ServerOnPowerChanged);
    }

    /// <summary>
    ///     Called when the server's power state changes
    /// </summary>
    private void ServerOnPowerChanged(EntityUid uid, HologramServerComponent component, ref PowerChangedEvent args)
    {
        // If the server loses power, kill the hologram
        if (!args.Powered && Exists(component.LinkedHologram))
        {
            _hologram.DoKillHologram(component.LinkedHologram.Value);
            component.LinkedHologram = null;
        }
    }
}
