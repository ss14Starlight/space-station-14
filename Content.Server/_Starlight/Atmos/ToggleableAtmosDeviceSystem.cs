using Content.Server.Atmos.EntitySystems;

namespace Content.Server._Starlight.Atmos;

public sealed partial class ToggleableAtmosDeviceSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;

    public override void Initialize() => base.Initialize();

    /// <summary>
    /// Sets the device to the boolean value
    /// </summary>
    public void Set(EntityUid uid, bool value) => RaiseLocalEvent(uid, new SetToggleSignalReceivedEvent(value));
    /// <summary>
    /// Toggles the device
    /// </summary>
    public void Toggle(EntityUid uid) => RaiseLocalEvent(uid, new ToggleSignalReceivedEvent());
}

public record struct SetToggleSignalReceivedEvent(bool Value);
public record struct ToggleSignalReceivedEvent();
