using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Abstract.Conditions;

public sealed partial class SubjectSpeciesCondition : BaseCondition
{
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>>? Whitelist;

    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>>? Blacklist;
    public override bool Handle(EntityUid @subject, EntityUid @object)
    {
        base.Handle(@subject, @object);
        return Ent.TryGetComponent<HumanoidAppearanceComponent>(@subject, out var appearance)
            && (Blacklist == null || !Blacklist.Contains(appearance.Species))
            && (Whitelist == null || Whitelist.Contains(appearance.Species));
    }
}
