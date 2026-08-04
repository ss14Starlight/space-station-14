using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Medical.Virology;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PathogenMildBlurComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Magnitude = 1.5f;
}

public sealed class PathogenMildBlurSystem : EntitySystem
{
    [Dependency] private BlurryVisionSystem _blurryVision = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PathogenMildBlurComponent, StatusEffectAppliedEvent>(OnChanged);
        SubscribeLocalEvent<PathogenMildBlurComponent, StatusEffectRemovedEvent>(OnChanged);
        SubscribeLocalEvent<PathogenMildBlurComponent, StatusEffectRelayedEvent<GetBlurEvent>>(OnGetBlur);
    }

    private void OnChanged(Entity<PathogenMildBlurComponent> entity, ref StatusEffectAppliedEvent args)
        => Refresh(args.Target);

    private void OnChanged(Entity<PathogenMildBlurComponent> entity, ref StatusEffectRemovedEvent args)
        => Refresh(args.Target);

    private void OnGetBlur(
        Entity<PathogenMildBlurComponent> entity,
        ref StatusEffectRelayedEvent<GetBlurEvent> args)
    {
        args.Args.Blur += entity.Comp.Magnitude;
    }

    private void Refresh(EntityUid target)
    {
        if (!TryComp<BlindableComponent>(target, out var blindable))
            return;

        _blurryVision.UpdateBlurMagnitude((target, blindable), blindable.IsWearingGlasses);
    }
}
