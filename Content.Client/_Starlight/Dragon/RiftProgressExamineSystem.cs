using Content.Client.Dragon;
using Content.Shared.Examine;

namespace Content.Client._Starlight.Dragon;

public sealed class RiftProgressExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DragonRiftComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DragonRiftComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("carp-rift-examine", ("percentage", ent.Comp.ProgressPercentage)));
    }
}
