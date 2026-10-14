using Content.Server.Silicons.Laws;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Server._Starlight.Silicons.Laws;

/// <summary>
/// Resolves the laws of a chassis a station AI has shunted into straight from the AI, so the two never
/// hold separate copies that can drift apart.
/// </summary>
public sealed partial class ShuntedSiliconLawSystem : EntitySystem
{
    [Dependency] private SiliconLawSystem _siliconLaw = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAIShuntComponent, GetSiliconLawsEvent>(OnGetLaws, before: [typeof(SiliconLawSystem)]);
    }

    private void OnGetLaws(Entity<StationAIShuntComponent> chassis, ref GetSiliconLawsEvent args)
    {
        if (args.Handled || chassis.Comp.Return is not { } ai || TerminatingOrDeleted(ai))
            return;

        args.Laws = _siliconLaw.GetLaws(ai);
        args.Handled = true;
    }
}
