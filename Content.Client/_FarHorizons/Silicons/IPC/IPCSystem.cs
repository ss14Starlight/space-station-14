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
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MobStateSystem _state = default!;

    protected override void UpdateThermals(float frameTime) {}
}