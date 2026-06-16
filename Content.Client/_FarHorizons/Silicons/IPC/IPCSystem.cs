using Content.Shared._FarHorizons.Silicons.IPC;
using Content.Shared.Alert;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem : SharedIPCSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MobStateSystem _state = default!;

    protected override void UpdateThermals(float frameTime) {}
}
