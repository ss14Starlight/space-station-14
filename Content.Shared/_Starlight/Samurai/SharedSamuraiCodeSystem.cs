using Content.Shared.Emag.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Dataset;

namespace Content.Shared._Starlight.Samurai;

public abstract partial class SharedSamuraiCodeSystem : EntitySystem
{

    public static readonly ProtoId<DatasetPrototype> BaseDataset = "SamuraiCodesBase";
    public static readonly ProtoId<DatasetPrototype> ErraticDataset = "SamuraiCodesErratic";
    public static readonly ProtoId<DatasetPrototype> HostileDataset = "SamuraiCodesHostile";

    [Dependency] private EmagSystem _emag = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SamuraiCodesComponent, GotEmaggedEvent>(OnEmagged);
    }

    protected virtual void OnEmagged(Entity<SamuraiCodesComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        // allow repeated-emagging of thaven
        // if (_emag.CheckFlag(ent, EmagType.Interaction))
        //     return;

        // allow self-emagging of thaven
        // if (ent.Owner == args.UserUid)
        //     return;

        args.Handled = true;
    }
}
