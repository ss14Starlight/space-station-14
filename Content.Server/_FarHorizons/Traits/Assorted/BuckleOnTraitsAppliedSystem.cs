using Content.Shared._FarHorizons.Traits;
using Content.Shared.Buckle;

namespace Content.Server._FarHorizons.Traits.Assorted;

public sealed partial class BuckleOnTraitsAppliedSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckleSystem = default!;

    [SubscribeLocalEvent]
    private void OnTraitsApplied(Entity<BuckleOnTraitsAppliedComponent> ent, ref TraitsApplied _)
    {
        #region Starlight
        var prototype = ent.Comp.Prototype;
        RemCompDeferred<BuckleOnTraitsAppliedComponent>(ent);

        if (_buckleSystem.IsBuckled(ent) || TerminatingOrDeleted(ent))
            return;

        var buckle = Spawn(prototype, Transform(ent).Coordinates);
        if (!_buckleSystem.TryBuckle(ent, ent, buckle))
            QueueDel(buckle);
        #endregion
    }
}
