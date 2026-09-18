using Content.Shared.Ensnaring.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Ensnaring;

public abstract partial class SharedEnsnareableSystem
{
    [SubscribeLocalEvent<EnsnaringComponent>]
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnsnaringComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, EnsnaringComponent component, ref StartCollideEvent args)
    {
        if (!component.CanImpactTrigger)
            return;

        if (TryEnsnare(args.OtherEntity, uid, component))
            _audio.PlayPvs(component.EnsnareSound, uid);
    }
}
