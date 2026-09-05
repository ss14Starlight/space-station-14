
using Robust.Shared.GameObjects;

namespace Content.Server._Starlight.Blooming;

public sealed class BloomingSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BloomingComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            comp.BloomAccumulator += frameTime;

            if (comp.BloomAccumulator < comp.BloomInterval)
                continue;

            comp.BloomAccumulator -= comp.BloomInterval;

            Bloom(uid, comp);
        }
    }

    private void Bloom(EntityUid uid, BloomingComponent comp)
    {
        // TODO: Emit pollen scent.
    }
}
