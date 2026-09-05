using Content.Server.Silicons.Laws;
using Content.Shared._Starlight.Silicons.Laws;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Silicons.Laws;

/// <summary>
/// Adds the "obey the station AI" law to silicons carrying <see cref="BorgObeysStationAiComponent"/>,
/// instead of it living inside a lawset that a lawboard could swap out.
/// </summary>
public sealed partial class BorgObeysStationAiSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgObeysStationAiComponent, GetSiliconLawsEvent>(OnGetLaws, after: [typeof(SiliconLawSystem)]);
    }

    private void OnGetLaws(Entity<BorgObeysStationAiComponent> borg, ref GetSiliconLawsEvent args)
    {
        // Only add to a lawset that a provider actually filled in.
        if (!args.Handled)
            return;

        // Emags, the FreeMAG and ion storms all mark the silicon subverted, and all of them take this law away.
        if (TryComp<SiliconLawProviderComponent>(borg, out var provider) && provider.Subverted)
            return;

        if (!_prototypes.TryIndex(borg.Comp.Law, out var law))
            return;

        // The lawset is cached on the provider, so this would otherwise stack up every time laws are read.
        if (args.Laws.Laws.Contains(law))
            return;

        args.Laws.Laws.Insert(0, law.ShallowClone());
    }
}
