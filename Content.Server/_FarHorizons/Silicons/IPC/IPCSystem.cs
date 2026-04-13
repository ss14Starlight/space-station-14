using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.EUI;
using Content.Server.Hands.Systems;
using Content.Server.Ninja.Systems;
using Content.Server.Popups;
using Content.Shared.PowerCell;
using Content.Server.Temperature.Systems;
using Content.Shared._FarHorizons.Silicons.IPC;
using Content.Shared.Alert;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Damage.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Inventory;
using Content.Server.Electrocution;
using Content.Server._Starlight.Medical.Body.Systems;

namespace Content.Server._FarHorizons.Silicons.IPC;

/// <inheritdoc/>
public sealed partial class IPCSystem : SharedIPCSystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly BatteryDrainerSystem _drainer = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly TemperatureSystem _tempSys = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;


    public override void Initialize()
    {
        base.Initialize();

        InitializeUI();
        InitializeWeld();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddVerbs);
    }

    private void AddVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract)
            return;

        AddBrainVerbs(ev);
        AddReviveVerbs(ev);
        AddRadioVerbs(ev);
    }
}
