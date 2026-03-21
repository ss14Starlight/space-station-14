using Content.Shared._Starlight.Shoelaces.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Clothing;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Shoelaces.Systems;

public sealed class SharedShoelacesSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> HardsuitTag = "Hardsuit";
    private static readonly ProtoId<TagPrototype> SuitEvaTag = "SuitEVA";

    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShoelaceTieableComponent, GetVerbsEvent<Verb>>(OnGetVerbs);

        SubscribeLocalEvent<ShoelaceTieableComponent, ShoelaceTieDoAfterEvent>(OnTieDoAfter);
        SubscribeLocalEvent<ShoelaceTieableComponent, DoAfterAttemptEvent<ShoelaceTieDoAfterEvent>>(OnTieDoAfterAttempt);
        SubscribeLocalEvent<ShoelaceTieableComponent, GotUnequippedEvent>(OnShoesUnequipped);
        SubscribeLocalEvent<ShoelaceTieableComponent, GotEquippedEvent>(OnShoesEquipped);
        SubscribeLocalEvent<ShoelaceTieableComponent, ShoelaceUntieDoAfterEvent>(OnUntieDoAfter);
        SubscribeLocalEvent<ShoelaceTieableComponent, DoAfterAttemptEvent<ShoelaceUntieDoAfterEvent>>(OnUntieDoAfterAttempt);

        SubscribeLocalEvent<ShoelaceTiedComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<ShoelaceTiedComponent, RemoveTiedShoelacesAlertEvent>(OnRemoveTiedAlert);
    }

    private void OnGetVerbs(Entity<ShoelaceTieableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        var parent = Transform(ent).ParentUid;

        if (CanManipulate(user, ent) && (ent.Comp.Tied || ent.Comp.TiedTogether))
        {
            if (user == parent)
            {
                var selfUntie = new Verb
                {
                    Text = Loc.GetString("shoelaces-verb-untie"),
                    Act = () => StartUntie(user, ent, true),
                };

                args.Verbs.Add(selfUntie);
                return;
            }
            else
            {
                var assistUntie = new Verb
                {
                    Text = Loc.GetString("shoelaces-verb-untie"),
                    Act = () => StartUntie(user, ent, false),
                };

                args.Verbs.Add(assistUntie);
                return;
            }
        }

        if (!CanManipulate(user, parent))
            return;

        if (IsTargetWearingTieBlockingSuit(parent))
            return;

        if (IsShoelaceTieBlocked(ent))
            return;

        var tieVerb = new Verb
        {
            Text = Loc.GetString("shoelaces-verb-tie"),
            Act = () => StartTie(user, ent, false),
        };

        var tieTogetherVerb = new Verb
        {
            Text = Loc.GetString("shoelaces-verb-tie-together"),
            Act = () => StartTie(user, ent, true),
        };

        args.Verbs.Add(tieVerb);
        args.Verbs.Add(tieTogetherVerb);
    }

    private bool CanManipulate(EntityUid user, EntityUid target)
    {
        if (!_actionBlocker.CanInteract(user, target))
            return false;

        return user == Transform(target).ParentUid || _interaction.InRangeAndAccessible(user, target, range: .5f);
    }

    private void StartTie(EntityUid user, Entity<ShoelaceTieableComponent> target, bool together)
    {
        if (!CanManipulate(user, target))
            return;

        if (IsTargetWearingTieBlockingSuit(target))
            return;

        if (IsShoelaceTieBlocked(target))
            return;

        var doAfter = new DoAfterArgs(EntityManager,
            user,
            target.Comp.TieTime,
            new ShoelaceTieDoAfterEvent(together),
            target,
            target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        if (together)
            _popup.PopupPredicted(Loc.GetString("shoelaces-popup-tying-together-start"), target, user, PopupType.Medium);
        else
            _popup.PopupPredicted(Loc.GetString("shoelaces-popup-tying-start"), target, user, PopupType.Medium);
    }

    private void OnTieDoAfterAttempt(Entity<ShoelaceTieableComponent> ent, ref DoAfterAttemptEvent<ShoelaceTieDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        var doAfterArgs = args.Event.Args;

        if (!CanManipulate(doAfterArgs.User, ent))
        {
            args.Cancel();
            return;
        }

        if (!_inventory.TryGetSlotEntity(ent, "shoes", out var shoes))
        {
            args.Cancel();
            return;
        }

        if (IsTargetWearingTieBlockingSuit(ent))
        {
            args.Cancel();
            return;
        }

        if (IsShoelaceTieBlocked(shoes.Value))
            args.Cancel();
    }

    private void OnTieDoAfter(Entity<ShoelaceTieableComponent> ent, ref ShoelaceTieDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!CanManipulate(args.Args.User, ent))
            return;

        var parentUid = Transform(ent).ParentUid;

        _alerts.ClearAlert(parentUid, ent.Comp.AlertUntied);
        if (!args.Together)
        {
            ent.Comp.Tied = true;
            Dirty(ent, ent.Comp);
            RemComp<ShoelaceTiedComponent>(parentUid);
            _popup.PopupPredicted(Loc.GetString("shoelaces-popup-tying-success-user"), args.Args.User, args.Args.User, PopupType.Medium);
            if (args.Args.User != parentUid)
                _popup.PopupPredicted(Loc.GetString("shoelaces-popup-tying-success-target", ("user", args.Args.User)), ent, null, Filter.Entities(parentUid), true, PopupType.MediumCaution);
        }
        else
        {
            ent.Comp.TiedTogether = true;
            Dirty(ent, ent.Comp);
            _alerts.ShowAlert(parentUid, ent.Comp.AlertTiedTogether);
            _popup.PopupPredicted(Loc.GetString("shoelaces-popup-tying-together-success-user"), args.Args.User, args.Args.User, PopupType.Medium);
            if (args.Args.User != parentUid)
                _popup.PopupPredicted(Loc.GetString("shoelaces-popup-tying-together-success-target", ("user", args.Args.User)), ent, null, Filter.Entities(parentUid), true, PopupType.MediumCaution);
        }

        args.Handled = true;
    }

    private void StartUntie(EntityUid user, Entity<ShoelaceTieableComponent> target, bool selfUntie)
    {
        if (!CanManipulate(user, target))
            return;

        var untieTime = selfUntie ? target.Comp.UntieSelfTime : target.Comp.UntieAssistTime;
        var doAfter = new DoAfterArgs(EntityManager,
            user,
            untieTime,
            new ShoelaceUntieDoAfterEvent(selfUntie),
            target,
            target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupPredicted(Loc.GetString("shoelaces-popup-untie-start"), target, user, PopupType.Medium);
    }

    private void OnUntieDoAfterAttempt(Entity<ShoelaceTieableComponent> ent, ref DoAfterAttemptEvent<ShoelaceUntieDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        var doAfterArgs = args.Event.Args;
        if (!CanManipulate(doAfterArgs.User, ent))
        {
            args.Cancel();
            return;
        }

        if (!ent.Comp.Tied && !ent.Comp.TiedTogether)
            args.Cancel();
    }

    private void OnUntieDoAfter(Entity<ShoelaceTieableComponent> ent, ref ShoelaceUntieDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var parent = Transform(ent).ParentUid;

        if (!CanManipulate(args.Args.User, ent))
            return;

        if (IsTargetWearingTieBlockingSuit(ent))
            return;

        if (IsShoelaceTieBlocked(ent.Owner))
            return;

        if (HasComp<MobStateComponent>(parent))
        {
            _alerts.ShowAlert(parent, ent.Comp.AlertUntied);
            _alerts.ClearAlert(parent, ent.Comp.AlertTiedTogether);
            EnsureComp<ShoelaceTiedComponent>(parent);
            ent.Comp.Tied = false;
            ent.Comp.TiedTogether = false;
        }
        _popup.PopupPredicted(Loc.GetString("shoelaces-popup-untie-success"), ent, args.Args.User, PopupType.Medium);

        args.Handled = true;
    }

    private void OnMoveInput(Entity<ShoelaceTiedComponent> ent, ref MoveInputEvent args)
    {
        if (!args.State || !args.HasDirectionalMovement)
            return;

        if (!args.Entity.Comp.Sprinting)
            return;

        if (ent.Comp.NextTripAttempt > _gameTiming.CurTime)
            return;

        ent.Comp.NextTripAttempt = _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp.TripAttemptCooldown);
        Dirty(ent, ent.Comp);

        if (ent.Comp.TiedShoes is not { } tiedShoes)
            return;

        _random.SetSeed(_gameTiming.CurTime.Seconds);
        if (!tiedShoes.Comp.Tied
            && _random.Prob(tiedShoes.Comp.KnockDownChance)
            && _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(ent.Comp.TripKnockdownTime), force: true))
            _popup.PopupPredicted(Loc.GetString("shoelaces-popup-trip"), ent, ent, PopupType.MediumCaution);
        else if (tiedShoes.Comp.TiedTogether && _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(ent.Comp.TripKnockdownTime), force: true))
        {
            if (_random.Prob(tiedShoes.Comp.ForceUntieChance))
            {
                _alerts.ShowAlert(ent.Owner, tiedShoes.Comp.AlertUntied);
                _alerts.ClearAlert(ent.Owner, tiedShoes.Comp.AlertTiedTogether);
                tiedShoes.Comp.TiedTogether = false;
                Dirty(tiedShoes, tiedShoes.Comp);
            }
            _popup.PopupPredicted(Loc.GetString("shoelaces-popup-trip"), ent, ent, PopupType.MediumCaution);
        }
    }

    private void OnRemoveTiedAlert(Entity<ShoelaceTiedComponent> ent, ref RemoveTiedShoelacesAlertEvent args)
    {
        if (args.Handled || ent.Comp.TiedShoes is not { } tiedShoes)
            return;

        if (!tiedShoes.Comp.Tied)
            StartTie(ent, ent.Comp.TiedShoes.Value, false);
        else if (tiedShoes.Comp.TiedTogether)
            StartUntie(ent, ent.Comp.TiedShoes.Value, true);
        args.Handled = true;
    }

    private void OnShoesUnequipped(Entity<ShoelaceTieableComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Slot != "shoes")
            return;

        _alerts.ClearAlert(args.Equipee, ent.Comp.AlertUntied);
        _alerts.ClearAlert(args.Equipee, ent.Comp.AlertTiedTogether);
        RemComp<ShoelaceTiedComponent>(args.Equipee);
    }

    private void OnShoesEquipped(Entity<ShoelaceTieableComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Slot != "shoes" || !_gameTiming.IsFirstTimePredicted)
            return;

        if (IsShoelaceTieBlocked(ent.Owner))
        {
            ent.Comp.Tied = false;
            Dirty(ent, ent.Comp);
        }

        var tied = EnsureComp<ShoelaceTiedComponent>(args.Equipee);
        tied.TiedShoes = ent;
        if (!ent.Comp.Tied)
            _alerts.ShowAlert(args.Equipee, ent.Comp.AlertUntied);
        else if (ent.Comp.TiedTogether)
            _alerts.ShowAlert(args.Equipee, ent.Comp.AlertTiedTogether);
    }

    private bool IsShoelaceTieBlocked(EntityUid shoes)
    {
        if (HasComp(shoes, typeof(MagbootsComponent)))
            return true;

        // Maybe you want to add more cases here in the future? Who knows!

        return false;
    }

    private bool IsTargetWearingTieBlockingSuit(EntityUid target)
    {
        if (!_inventory.TryGetSlotEntity(target, "outerClothing", out var outerClothing))
            return false;

        return _tag.HasTag(outerClothing.Value, HardsuitTag)
               || _tag.HasTag(outerClothing.Value, SuitEvaTag);
    }
}
