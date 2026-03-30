using Content.Shared._FarHorizons.Silicons.IPC;
using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.CCVar;
using Content.Shared.Database;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    // CCvar.
    private int _maxNameLength;

    private void InitializeUI()
    {
        SubscribeLocalEvent<IPCUserInterfaceComponent, IPCEjectBrainBuiMessage>(OnEjectBrainBuiMessage);
        SubscribeLocalEvent<IPCUserInterfaceComponent, IPCEjectBatteryBuiMessage>(OnEjectBatteryBuiMessage);
        SubscribeLocalEvent<IPCUserInterfaceComponent, IPCSetNameBuiMessage>(OnSetNameBuiMessage);
        SubscribeLocalEvent<IPCUserInterfaceComponent, BoundUIOpenedEvent>(OnUIOpened);

        Subs.CVar(_cfgManager, CCVars.MaxNameLength, value => _maxNameLength = value, true);
    }

    protected override void UpdateUI(float frameTime)
    {
        var query = EntityQueryEnumerator<IPCUserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, IPCUiKey.Key)) continue;

            if (_timing.CurTime < comp.NextUpdate) continue;

            comp.NextUpdate = _timing.CurTime + comp.RefreshRate;

            UpdateUIHealth(uid);
        }
    }

    private void OnUIOpened(Entity<IPCUserInterfaceComponent> ent, ref BoundUIOpenedEvent args) =>
        UpdateUIHealth(ent.Owner);
        
    private void UpdateUIHealth(EntityUid ent)
    {
        var healthMessage = new IPCHealthMessage(_bloodstream.GetBloodLevel(ent));

        _ui.ServerSendUiMessage(ent, IPCUiKey.Key, healthMessage);
    }

    private void OnEjectBrainBuiMessage(Entity<IPCUserInterfaceComponent> ent, ref IPCEjectBrainBuiMessage args)
    {
        if (!TryComp<IPCLockComponent>(ent.Owner, out var lockComp)) return;

        if (lockComp.Lock.Locked || !lockComp.WiresPanel.Open)
        {
            _popup.PopupEntity(Loc.GetString(lockComp.LockedPopupMessage), ent);
            _audio.PlayPvs(lockComp.LockedSound, ent);
            return;
        }

        EjectBrain(ent.Owner, args.Actor);
    }

    private void OnEjectBatteryBuiMessage(Entity<IPCUserInterfaceComponent> ent, ref IPCEjectBatteryBuiMessage args)
    {
        if (!TryComp<IPCLockComponent>(ent.Owner, out var lockComp)) return;

        if (lockComp.Lock.Locked || !lockComp.WiresPanel.Open)
        {
            _popup.PopupEntity(Loc.GetString(lockComp.LockedPopupMessage), ent);
            _audio.PlayPvs(lockComp.LockedSound, ent);
            return;
        }

        EjectBattery(ent.Owner, args.Actor);
    }
    private void OnSetNameBuiMessage(Entity<IPCUserInterfaceComponent> ent, ref IPCSetNameBuiMessage args)
    {
        if (args.Name.Length > _maxNameLength ||
            args.Name.Length == 0 ||
            string.IsNullOrWhiteSpace(args.Name) ||
            string.IsNullOrEmpty(args.Name))
            return;

        var name = args.Name.Trim();

        var metaData = MetaData(ent);

        if (metaData.EntityName.Equals(name, StringComparison.InvariantCulture))
            return;

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.Actor):player} set IPC \"{ToPrettyString(ent)}\"'s name to: {name}");
        _metaData.SetEntityName(ent, name, metaData, false);
    }
} 