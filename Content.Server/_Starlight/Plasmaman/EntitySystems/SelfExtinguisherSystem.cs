using Content.Server.Atmos.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Atmos.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Effects;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared._Starlight.Plasmaman.Components;
using Content.Shared._Starlight.Plasmaman.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Collections.Generic;

namespace Content.Server._Starlight.Plasmaman.EntitySystems;

public sealed partial class SelfExtinguisherSystem : SharedSelfExtinguisherSystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private readonly Color _extinguishColor = Color.FromHex("#75b1f0");
    private TimeSpan _nextAutoUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SelfExtinguisherComponent, SelfExtinguishEvent>(OnSelfExtinguish);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextAutoUpdate)
            return;

        _nextAutoUpdate = curTime + TimeSpan.FromSeconds(0.25);

        var query = EntityQueryEnumerator<SelfExtinguisherComponent>();
        while (query.MoveNext(out var uid, out var extinguisher))
        {
            if (!_inventory.TryGetContainingSlot((uid, null, null), out _))
                continue;

            if (!_container.TryGetContainingContainer((uid, null, null), out var container) ||
                !TryComp<FlammableComponent>(container.Owner, out var flammable) ||
                !flammable.OnFire)
            {
                continue;
            }

            TryExtinguish(container.Owner, uid, extinguisher, silentFailure: true, flammable);
        }
    }

    private void OnSelfExtinguish(Entity<SelfExtinguisherComponent> ent, ref SelfExtinguishEvent args)
    {
        TryExtinguish(args.Performer, ent.Owner, ent.Comp, silentFailure: false);
    }

    private void TryExtinguish(EntityUid user, EntityUid uid, SelfExtinguisherComponent selfExtinguisher, bool silentFailure, FlammableComponent? flammable = null)
    {
        if (!_container.TryGetContainingContainer((uid, null, null), out var container) ||
            !TryComp(container.Owner, out flammable))
        {
            return;
        }

        var target = container.Owner;
        var curTime = _timing.CurTime;
        if (TryComp<LimitedChargesComponent>(uid, out var charges) && _charges.IsEmpty((uid, charges)))
        {
            if (selfExtinguisher.ActionEntity is { } emptyAction)
                _actions.SetEnabled(emptyAction, false);

            if (silentFailure || !SetPopupCooldown((uid, selfExtinguisher), curTime))
                return;

            _popup.PopupEntity(Loc.GetString("self-extinguisher-no-charges", ("item", uid)),
                target, user, PopupType.MediumCaution);

            return;
        }

        if (selfExtinguisher.ActionEntity is { } readyAction)
            _actions.SetEnabled(readyAction, true);

        if (selfExtinguisher.NextExtinguish > curTime)
        {
            if (silentFailure || !SetPopupCooldown((uid, selfExtinguisher), curTime))
                return;

            _popup.PopupEntity(Loc.GetString("self-extinguisher-on-cooldown", ("item", uid)),
                target, user, PopupType.MediumCaution);
            return;
        }

        if (!flammable.OnFire)
        {
            if (silentFailure || !SetPopupCooldown((uid, selfExtinguisher), curTime))
                return;

            _popup.PopupEntity(Loc.GetString("self-extinguisher-not-on-fire-self"),
                target, user);
            return;
        }

        _flammable.Extinguish(target, flammable: flammable);
        _color.RaiseEffect(_extinguishColor, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
        _audio.PlayPvs(selfExtinguisher.Sound, uid, selfExtinguisher.Sound.Params.WithVariation(0.125f));
        _popup.PopupEntity(Loc.GetString("self-extinguisher-extinguish-self", ("item", uid)),
            target, user, PopupType.Medium);

        selfExtinguisher.NextExtinguish = curTime + selfExtinguisher.Cooldown;
        Dirty(uid, selfExtinguisher);

        if (charges != null)
        {
            _charges.TryUseCharge((uid, charges));

            if (_charges.IsEmpty((uid, charges)))
            {
                if (selfExtinguisher.ActionEntity is { } emptyAction)
                    _actions.SetEnabled(emptyAction, false);
                return;
            }
        }

        if (selfExtinguisher.ActionEntity is { } action)
            _actions.StartUseDelay(action);
    }
}
