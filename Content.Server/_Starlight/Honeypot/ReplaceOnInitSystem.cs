using Content.Server._Starlight.Honeypot.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Honeypot;

/// <summary>
/// Replaces entities marked with <see cref="ReplaceOnInitComponent"/> on map init with the specified prototype and overrides.
/// </summary>
[EntityCategory("Spawner")]
public sealed partial class ReplaceOnInitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReplaceOnInitComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ReplaceOnInitComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        var spawned = SpawnAtPosition(ent.Comp.Proto, xform.Coordinates, ent.Comp.Overrides);
        Transform(spawned).LocalRotation = xform.LocalRotation;

        QueueDel(ent);
    }
}
