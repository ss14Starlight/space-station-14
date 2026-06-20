using Content.Server.Atmos.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Server.Stunnable;
using Content.Shared._Starlight.Devil;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Devil;

public sealed partial class DevilSystem : SharedDevilSystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private StunSystem _stun = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private void SubscribeDamned()
    {
        SubscribeLocalEvent<DamnedComponent, EntitySpokeEvent>(OnEntitySpoke);
    }

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
        _audio.PlayPvs(damned.DamnedPunishmentSound, uid);
    }
}
