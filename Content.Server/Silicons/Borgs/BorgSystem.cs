using Content.Server.Administration.Managers;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emag.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Roles;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
// Starlight Start
using Content.Shared.Silicons.Borgs.Components;
using Content.Server.Chat.Systems;
// Starlight End

namespace Content.Server.Silicons.Borgs;

/// <inheritdoc/>
public sealed partial class BorgSystem : SharedBorgSystem
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;

    public static readonly ProtoId<JobPrototype> BorgJobId = "Borg";

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        // Starlight Start: Allow borgs to LOOC while crit
        SubscribeLocalEvent<BorgChassisComponent, LoocCritCheckEvent>(OnLoocCritCheckChassis);
        SubscribeLocalEvent<BorgBrainComponent, LoocCritCheckEvent>(OnLoocCritCheckBrain);
        // Starlight End

        InitializeTransponder();
    }

    public override bool CanPlayerBeBorged(ICommonSession session)
    {
        if (_banManager.GetJobBans(session.UserId)?.Contains(BorgJobId) == true)
            return false;

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateTransponder(frameTime);
    }

    // Starlight Start: Allow borgs to LOOC while crit
    private void OnLoocCritCheckChassis(EntityUid uid, BorgChassisComponent component, LoocCritCheckEvent args) =>
        args.AllowCritLooc = true;

    private void OnLoocCritCheckBrain(EntityUid uid, BorgBrainComponent component, LoocCritCheckEvent args) =>
        args.AllowCritLooc = true;
    // Starlight End
}
