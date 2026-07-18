using Content.Shared.Eye.Blinding.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared.Eye.Blinding.Systems;

public sealed partial class TemporaryBlindnessSystem : EntitySystem
{
    public static readonly EntProtoId BlindingStatusEffect = "StatusEffectTemporaryBlindness";

    [Dependency] private BlindableSystem _blindableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryBlindnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TemporaryBlindnessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TemporaryBlindnessComponent, CanSeeAttemptEvent>(OnBlindTrySee);

        // Component lives on the status-effect entity; mirror it onto the applied target for gameplay events.
        SubscribeLocalEvent<TemporaryBlindnessComponent, StatusEffectAppliedEvent>((_, _, ref args) => EnsureComp<TemporaryBlindnessComponent>(args.Target));
        SubscribeLocalEvent<TemporaryBlindnessComponent, StatusEffectRemovedEvent>((_, _, ref args) => RemComp<TemporaryBlindnessComponent>(args.Target));
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
