using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Body.Events;
using Content.Shared.Database;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Containers;

namespace Content.Server._FarHorizons.Silicons.IPC;

public sealed partial class IPCSystem
{
    protected override void SetupBrain()
    {
        base.SetupBrain();

        SubscribeLocalEvent<IPCBrainHolderComponent, AfterInteractUsingEvent>(OnBrainInteractUsing);
        SubscribeLocalEvent<IPCBrainHolderComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<IPCBrainHolderComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<IPCBrainHolderComponent, BeingGibbedEvent>(OnBrainGibbed);
        SubscribeLocalEvent<IPCBrainComponent, MindAddedMessage>(OnBrainMindAdded);
    }

    private void OnBrainGibbed(Entity<IPCBrainHolderComponent> ent, ref BeingGibbedEvent args) =>
        _container.EmptyContainer(ent.Comp.BrainContainerSlot);

    private void OnInserted(Entity<IPCBrainHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!HasComp<IPCBrainComponent>(args.Entity) ||
            !_mind.TryGetMind(args.Entity, out var mindId, out var mind) ||
            args.Container != ent.Comp.BrainContainerSlot)
            return;

        _mind.TransferTo(mindId, ent, mind: mind);
    }

    private void OnRemoved(Entity<IPCBrainHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<IPCBrainComponent>(args.Entity) ||
            !_mind.TryGetMind(ent, out var mindId, out var mind) ||
            args.Container != ent.Comp.BrainContainerSlot)
            return;

        _mind.TransferTo(mindId, args.Entity, mind: mind);
    }

    private void OnBrainMindAdded(Entity<IPCBrainComponent> ent, ref MindAddedMessage args)
    {
        if (!_container.TryGetOuterContainer(ent, Transform(ent), out var container) ||
            !TryComp(container.Owner, out IPCBrainHolderComponent? brainComp) ||
            container.ID != brainComp.BrainContainerSlotID ||
            !_mind.TryGetMind(ent, out var mindId, out var mind) ||
            !_player.TryGetSessionById(mind.UserId, out var session))
            return;

        _mind.TransferTo(mindId, container.Owner, mind: mind);
    }

    private void OnBrainInteractUsing(Entity<IPCBrainHolderComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled ||
            !TryComp(args.Used, out BorgBrainComponent? brain)
            || HasComp<StationAIShuntComponent>(args.Used)) // Yeah, No "AI" IPC...
            return;

        if (TryComp<WiresPanelComponent>(ent, out var panel) && !panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("borg-panel-not-open"), ent, args.User);
            return;
        }

        if (ent.Comp.BrainEntity == null)
        {
            _container.Insert(args.Used, ent.Comp.BrainContainerSlot);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(args.User):player} installed brain {ToPrettyString(args.Used)} into IPC {ToPrettyString(ent)}");
            _popup.PopupEntity(Loc.GetString("ipc-brain-inserted"), ent, args.User);
            _audio.PlayPvs(_audio.ResolveSound(ent.Comp.BrainInsertionSound), ent);
            args.Handled = true;
        }
    }

    private void AddBrainVerbs(GetVerbsEvent<Verb> ev)
    {
        if (!TryComp<IPCBrainHolderComponent>(ev.Target, out var brain) ||
            !TryComp<WiresPanelComponent>(ev.Target, out var wires) ||
            !wires.Open)
            return;

        var verb = new Verb
        {
            Text = "Brain",
            Category = VerbCategory.Eject,
            IconEntity = GetNetEntity(brain.BrainEntity),
            Act = () => EjectBrain(ev.Target, ev.User),
        };

        ev.Verbs.Add(verb);
    }

    public void EjectBrain(Entity<IPCBrainHolderComponent?> target, EntityUid user){
        if (!Resolve(target, ref target.Comp) ||
            target.Comp.BrainEntity == null)
            return;

        if (target.Owner == user)
        {
            _popup.PopupEntity(Loc.GetString("ipc-cant-eject-own-brain"), user, user);
            return;
        }

        var brain = target.Comp.BrainEntity.Value;
        _container.EmptyContainer(target.Comp.BrainContainerSlot, reparent: false);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):player} ejected brain from IPC {ToPrettyString(target)}");
        _hands.PickupOrDrop(user, brain, dropNear: true);

        _popup.PopupEntity(Loc.GetString("ipc-brain-ejected"), target, user);
        _audio.PlayPvs(_audio.ResolveSound(target.Comp.BrainExtractionSound), target);
    }
}
