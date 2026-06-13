using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Administration.Systems;

public sealed class RejuvenateSystem : EntitySystem
{
    // starlight start - add instant action handler for rejuvenate action
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private SoundPathSpecifier Sound = new("/Audio/_Starlight/Misc/rejuvenate.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, RejuvenateInstantActionEvent>(OnRejuvenateInstantEvent);
    }

    private void OnRejuvenateInstantEvent(Entity<ActionsComponent> ent, ref RejuvenateInstantActionEvent args)
    {
        if (TryComp<DamageableComponent>(args.Performer, out var damageable)) {
            Dictionary<string, FixedPoint2> preservedDamage = new();
            foreach (var damageType in args.PreserveDamageTypes)
            {
                if (damageable.Damage.DamageDict.TryGetValue(damageType, out var damage))
                    preservedDamage[damageType] = damage;
            }
            PerformRejuvenate(args.Performer);
            _damageable.TryChangeDamage(args.Performer, new() { DamageDict = preservedDamage }, ignoreResistances: true);
        } else PerformRejuvenate(args.Performer);

        _popup.PopupPredicted(Loc.GetString("entity-rejuvenated-popup", ("name", Name(args.Performer))), args.Performer, args.Performer, PopupType.LargeCaution);
        _audio.PlayPredicted(Sound, args.Performer, args.Performer);
        args.Handled = true;
    }
    // starlight end

    /// <summary>
    /// Fully heals the target, removing all damage, debuffs or other negative status effects.
    /// </summary>
    public void PerformRejuvenate(EntityUid target)
    {
        RaiseLocalEvent(target, new RejuvenateEvent());
    }
}

// starlight start
/// <summary>
/// Instant action to rejuvenate self
/// </summary>
[UsedImplicitly]
public sealed partial class RejuvenateInstantActionEvent : InstantActionEvent
{
    [DataField]
    public List<string> PreserveDamageTypes = [];
};
// starlight end
