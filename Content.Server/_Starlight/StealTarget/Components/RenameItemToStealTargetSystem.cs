using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.StealTarget.Components;

public sealed class RenameItemToStealTargetSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RenameItemToStealTargetComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, RenameItemToStealTargetComponent component, ComponentStartup args)
    {
        if (!TryComp<StealTargetComponent>(uid, out var stealTarget))
        {
            Log.Error($"Steal target not attached!");
            return;
        }

        ProtoId<StealTargetGroupPrototype> protoId = stealTarget.StealGroup;
        var stealGroupProto = _proto.Index(protoId);

        _metaData.SetEntityName(uid, stealGroupProto.Name);
    }

}
