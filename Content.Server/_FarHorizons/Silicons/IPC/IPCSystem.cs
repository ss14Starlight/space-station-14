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
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private BatteryDrainerSystem _drainer = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private MobStateSystem _state = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private TemperatureSystem _tempSys = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private ElectrocutionSystem _electrocution = default!;


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
