using Content.Shared.Eye.Blinding.Components;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed class TemporaryBlindnessSystem : EntitySystem
{
    public static readonly ProtoId<StatusEffectPrototype> BlindingStatusEffect = "TemporaryBlindness";

    [Dependency] private readonly BlindableSystem _blindableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryBlindnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TemporaryBlindnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TemporaryBlindnessComponent, CanSeeAttemptEvent>(OnBlindTrySee);

        // Starlight - Start
        // ! REMOVE THIS SHIT WHEN WIZDEN ACTULLY MIGRADE THE FUCKING STATUS EFFECT THINGY GOSH DAMN!
        SubscribeLocalEvent<TemporaryBlindnessComponent, StatusEffectAppliedEvent>((_, _, ref args) => EnsureComp<TemporaryBlindnessComponent>(args.Target));
        SubscribeLocalEvent<TemporaryBlindnessComponent, StatusEffectRemovedEvent>((_, _, ref args) => RemComp<TemporaryBlindnessComponent>(args.Target));
        // Starlight - End
    }

    private void OnStartup(EntityUid uid, TemporaryBlindnessComponent component, ComponentStartup args)
    {
        _blindableSystem.UpdateIsBlind(uid);
    }

    private void OnShutdown(EntityUid uid, TemporaryBlindnessComponent component, ComponentShutdown args)
    {
        _blindableSystem.UpdateIsBlind(uid);
    }

    private void OnBlindTrySee(EntityUid uid, TemporaryBlindnessComponent component, CanSeeAttemptEvent args)
    {
        if (component.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }
}
