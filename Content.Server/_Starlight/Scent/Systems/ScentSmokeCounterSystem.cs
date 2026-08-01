using Content.Shared._Starlight.Scent.Systems;
using Content.Shared._Starlight.Scent.Components;
using Content.Shared.Chemistry.Components;

namespace Content.Server._Starlight.Scent.Systems;

// Forces a sneeze on smoke contact, regardless of what the smoke contains.
public sealed class ScentSmokeCounterSystem : EntitySystem
{
    [Dependency] private readonly SharedScentSystem _scent = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokeAffectedComponent, ComponentStartup>(OnEnterSmoke);
    }

    private void OnEnterSmoke(Entity<SmokeAffectedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SmellerComponent>(ent.Owner, out var smeller))
            return;

        _scent.ForceSneeze((ent.Owner, smeller), smeller.SmokeLockout);
    }
}
