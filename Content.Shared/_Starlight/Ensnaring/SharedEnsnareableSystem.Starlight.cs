// ReSharper disable CheckNamespace
using Content.Shared.Ensnaring.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Ensnaring;

public abstract partial class SharedEnsnareableSystem
{
    [SubscribeLocalEvent]
    private void OnStartCollide(EntityUid uid, EnsnaringComponent component, ref StartCollideEvent args)
    {
        if (!component.CanImpactTrigger)
            return;

        if (TryEnsnare(args.OtherEntity, uid, component))
            _audio.PlayPvs(component.EnsnareSound, uid);
    }
}
