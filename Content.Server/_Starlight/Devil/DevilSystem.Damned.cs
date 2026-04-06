using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Server.Stunnable;
using Content.Shared._Starlight.Devil;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly StunSystem _stun = default!;

    private SoundSpecifier DamnedPunishmentSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
    private void SubscribeDamned()
    {
        SubscribeLocalEvent<DamnedComponent, DamnationInitFailEvent>(OnDamnationInitFail);
        SubscribeLocalEvent<DamnedComponent, ComponentShutdown>(OnDamnationShutdown);
        SubscribeLocalEvent<DamnedComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

    private bool CanDamn(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto) => !entity.Comp.Damnations.Contains(proto);

    private bool AddDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> proto)
    {
        // here we shove all the components in, and then await their potential fails later via the event
        if (!CanDamn(entity, proto)) return false;
        if (!_proto.TryIndex(proto, out var damnationPrototype)) return false;

        EntityManager.AddComponents(entity.Owner, damnationPrototype.Components);
        EntityManager.RemoveComponents(entity.Owner, damnationPrototype.RemovedComponents);

        foreach (var action in damnationPrototype.Actions)
        {
            if (!action.IocResolved)
            {
                action.ResolveIoC();
                action.IocResolved = true;
            }

            if (!action.Action(entity)) return false;
        }

        entity.Comp.NetCost += damnationPrototype.Cost;
        entity.Comp.Damnations.Add(proto);

        return true;
    }

    private bool DamnEntity(EntityUid ent, InfernalContractData contract, EntityUid devil)
    {
        EnsureComp<DamnedComponent>(ent, out var damnedComp);

        damnedComp.DamnedBy = devil;

        // we add here instead of component startup so that we can know the devil's uid
        if (TryComp<DevilComponent>(devil, out var devilComponent) && contract.Damnations.Contains(devilComponent.SoulDamnation))
        {
            devilComponent.DamnedSouls.Add(ent);

            var ev = new DevilSoulsDamnedCountChangedEvent();
            RaiseLocalEvent(devil, ref ev);
        }

        // check to see that all of the damnations will work, before we try to add any
        foreach (var damnation in contract.Damnations)
            if (!CanDamn((ent, damnedComp), damnation)) return false;

        foreach (var damnation in contract.Damnations)
        {
            if(!AddDamnation((ent, damnedComp), damnation))
            {
                var ev = new DamnationInitFailEvent();
                RaiseLocalEvent(ent, ref ev);
                return false;
            }
        }

        _popup.PopupEntity(Loc.GetString("devil-popup-damnation", ("name", Name(ent))), ent, Shared.Popups.PopupType.MediumCaution);

        return true;
    }

    private bool RemoveDamnation(Entity<DamnedComponent> entity, ProtoId<DamnationPrototype> damnation)
    {
        if (!entity.Comp.Damnations.Contains(damnation)) return false;
        if (!_proto.TryIndex(damnation, out var damnationPrototype)) return false;

        if (damnationPrototype.ReverseOnRemove)
        {
            EntityManager.RemoveComponents(entity.Owner, damnationPrototype.Components);
            EntityManager.AddComponents(entity.Owner, damnationPrototype.RemovedComponents);
        }

        foreach (var action in damnationPrototype.Actions)
        {
            if (!action.IocResolved) {
                action.ResolveIoC();
                action.IocResolved = true;
            }

            action.ReverseAction(entity);
        }

        entity.Comp.Damnations.Remove(damnation);

        return true;
    }

    /// <summary>
    /// If this event is triggered, a damnation has failed to apply, so we need to reverse them all
    /// </summary>
    private void OnDamnationInitFail(Entity<DamnedComponent> ent, ref DamnationInitFailEvent args)
    {
        var damnations = new List<ProtoId<DamnationPrototype>>(ent.Comp.Damnations);
        foreach (var damnation in damnations)
            RemoveDamnation(ent, damnation);
        RemComp<DamnedComponent>(ent.Owner);

        _popup.PopupEntity(Loc.GetString("devil-popup-damnation-fail"), ent.Owner, PopupType.Small);
    }

    private void OnDamnationShutdown(Entity<DamnedComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<DevilComponent>(ent.Comp.DamnedBy, out var devilComp)) {
            devilComp.DamnedSouls.Remove(ent.Owner);
            var ev = new DevilSoulsDamnedCountChangedEvent();
            RaiseLocalEvent(ent.Comp.DamnedBy, ref ev);
        }
    }

    // misc section
    private void OnEntitySpoke(EntityUid uid, DamnedComponent damned, EntitySpokeEvent args)
    {
        if (!TryComp<DevilComponent>(damned.DamnedBy, out var devil)) return;
        if (!args.Message.OriginalText.Contains(devil.TrueName, StringComparison.InvariantCultureIgnoreCase)) return;

        // damned person spoke the devil's name, fire time
        _flammable.AdjustFireStacks(uid, 10f);
        _flammable.Ignite(uid, uid);
        _stun.TryKnockdown(uid, TimeSpan.FromSeconds(5));
        _popup.PopupEntity(Loc.GetString("damned-attempts-utter-name", ("name", Name(uid))), uid, PopupType.LargeCaution);
        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Heat", 150);
        _damageable.TryChangeDamage(uid, dspec, true);
        _audio.PlayPvs(DamnedPunishmentSound, uid);
    }
}
