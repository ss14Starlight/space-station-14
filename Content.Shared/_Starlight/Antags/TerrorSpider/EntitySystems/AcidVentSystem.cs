using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Antags.TerrorSpider.EntitySystems;

public sealed partial class AcidVentSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private WeldableSystem _weldable = default!;

    private static readonly ProtoId<TagPrototype> GasVentTag = "GasVent";

    public override void Initialize()
    {
        SubscribeLocalEvent<AcidVentEvent>(OnAcidVent);
    }

    private void OnAcidVent(AcidVentEvent args)
    {
        if (!_tag.HasTag(args.Target, GasVentTag))
            return;

        args.Handled = true;

        _weldable.SetWeldedState(args.Target, false);
    }
}
