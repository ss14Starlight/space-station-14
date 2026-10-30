using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.StationRadio.Systems;

public sealed partial class RecordCrateSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private DiscCoverSystem _cover = default!;

    [SubscribeLocalEvent]
    private void OnInit(Entity<RecordCrateComponent> ent, ref ComponentInit _) =>
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);

    private Container Records(Entity<RecordCrateComponent> ent) =>
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<RecordCrateComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_whitelist.IsWhitelistPass(ent.Comp.Whitelist, args.Used))
            return;

        args.Handled = true;
        var records = Records(ent);
        if (records.Count >= ent.Comp.Capacity)
        {
            _popup.PopupClient(Loc.GetString("record-crate-full"), ent.Owner, args.User);
            return;
        }

        if (_container.Insert(args.Used, records))
            _audio.PlayPredicted(ent.Comp.InsertSound, ent.Owner, args.User);
    }

    [SubscribeLocalEvent]
    private void OnInteractHand(Entity<RecordCrateComponent> ent, ref InteractHandEvent args)
    {
        var records = Records(ent);
        if (args.Handled || records.Count == 0)
            return;

        TakeOut(ent, records.ContainedEntities[^1], args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<RecordCrateComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        var records = Records(ent);
        if (!args.CanAccess || !args.CanInteract || records.Count == 0)
            return;

        var user = args.User;
        var priority = 0;
        for (var i = records.Count - 1; i >= 0; i--)
        {
            var record = records.ContainedEntities[i];
            args.Verbs.Add(new Verb
            {
                Text = Name(RecordOf(record)),
                Category = VerbCategory.Eject,
                Priority = priority--,
                Act = () => TakeOut(ent, record, user),
            });
        }
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<RecordCrateComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("record-crate-examine",
            ("count", Records(ent).Count),
            ("capacity", ent.Comp.Capacity)));
    }

    private void TakeOut(Entity<RecordCrateComponent> ent, EntityUid record, EntityUid user)
    {
        var records = Records(ent);
        if (!records.Contains(record) || !_container.Remove(record, records))
            return;

        _hands.PickupOrDrop(user, record);
        _audio.PlayPredicted(ent.Comp.RemoveSound, ent, user);
    }

    private EntityUid RecordOf(EntityUid item) =>
        TryComp<DiscCoverComponent>(item, out var cover) && _cover.GetDisc((item, cover)) is { } disc ? disc : item;
}
