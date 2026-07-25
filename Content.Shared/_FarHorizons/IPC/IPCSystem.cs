using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.Silicons.IPC;

/// <summary>
/// This handles all logic related to IPC. The system is modular, you can pick and choose components independently from each other
/// </summary>
public abstract partial class SharedIPCSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ItemSlotsSystem _items = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);

        SetupThermals();
        SetupBrain();
        SetupRevive();
        SetupBattery();
        SetupRadio();
        SetupLock();
    }

    private void AddAltVerbs(GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract)
            return;
        AddLockAltVerbs(ev);
        AddBatteryAltVerbs(ev);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateBattery(frameTime);
        UpdateThermals(frameTime);
        UpdateUI(frameTime);
    }

    protected virtual void UpdateUI(float frameTime) { }
}
