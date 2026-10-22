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
    private void OnInit(EntityUid uid, RecordCrateComponent comp, ComponentInit args) =>
        _container.EnsureContainer<Container>(uid, comp.ContainerId);

    private BaseContainer Records(Entity<RecordCrateComponent> ent) =>
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);

    [SubscribeLocalEvent]
    private void OnInteractUsing(EntityUid uid, RecordCrateComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !_whitelist.IsWhitelistPass(comp.Whitelist, args.Used))
            return;

        args.Handled = true;
        var records = Records((uid, comp));
        if (records.Count >= comp.Capacity)
        {
            _popup.PopupClient(Loc.GetString("record-crate-full"), uid, args.User);
            return;
        }

        if (_container.Insert(args.Used, records))
            _audio.PlayPredicted(comp.InsertSound, uid, args.User);
    }

    [SubscribeLocalEvent]
    private void OnInteractHand(EntityUid uid, RecordCrateComponent comp, InteractHandEvent args)
    {
        var records = Records((uid, comp));
        if (args.Handled || records.Count == 0)
            return;

        TakeOut((uid, comp), records.ContainedEntities[^1], args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(EntityUid uid, RecordCrateComponent comp, GetVerbsEvent<Verb> args)
    {
        var records = Records((uid, comp));
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
                Act = () => TakeOut((uid, comp), record, user),
            });
        }
    }

    [SubscribeLocalEvent]
    private void OnExamined(EntityUid uid, RecordCrateComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("record-crate-examine",
            ("count", Records((uid, comp)).Count),
            ("capacity", comp.Capacity)));
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
