using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Administration.Systems;

public sealed partial class RejuvenateSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private readonly SoundPathSpecifier _sound = new("/Audio/_Starlight/Misc/rejuvenate.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, RejuvenateInstantActionEvent>(OnRejuvenateInstantEvent);
    }

    private void OnRejuvenateInstantEvent(Entity<ActionsComponent> ent, ref RejuvenateInstantActionEvent args)
    {
        if (TryComp<DamageableComponent>(args.Performer, out var damageable)) {
            Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> preservedDamage = new();
            foreach (var damageType in args.PreserveDamageTypes)
            {
                if (damageable.Damage.DamageDict.TryGetValue(damageType, out var damage))
                    preservedDamage[damageType] = damage;
            }
            PerformRejuvenate(args.Performer);
            _damageable.TryChangeDamage(args.Performer, new() { DamageDict = preservedDamage }, ignoreResistances: true);
        } else PerformRejuvenate(args.Performer);

        _popup.PopupPredicted(Loc.GetString("entity-rejuvenated-popup", ("name", Name(args.Performer))), args.Performer, args.Performer, PopupType.LargeCaution);
        _audio.PlayPredicted(_sound, args.Performer, args.Performer);
        args.Handled = true;
    }
}

/// <summary>
/// Instant action to rejuvenate self
/// </summary>
[UsedImplicitly]
public sealed partial class RejuvenateInstantActionEvent : InstantActionEvent
{
    [DataField]
    public List<ProtoId<DamageTypePrototype>> PreserveDamageTypes = [];
};
