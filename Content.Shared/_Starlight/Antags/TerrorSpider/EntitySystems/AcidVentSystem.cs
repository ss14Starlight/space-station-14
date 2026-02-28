using Content.Shared.Tag;
using Content.Shared.Tools.Systems;

namespace Content.Shared._Starlight.Antags.TerrorSpider;

public sealed class AcidVentSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<AcidVentEvent>(OnAcidVent);
    }

    private void OnAcidVent(AcidVentEvent args)
    {
        if (!_tag.HasTag(args.Target, "GasVent"))
            return;

        args.Handled = true;

        _weldable.SetWeldedState(args.Target, false);
    }
}