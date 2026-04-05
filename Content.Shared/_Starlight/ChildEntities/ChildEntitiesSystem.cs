using Content.Shared.Buckle.Components;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Railroading;

public sealed partial class ChildEntitiesSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChildEntitiesComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ChildEntitiesComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<ChildEntitiesComponent> ent, ref MapInitEvent args)
    {
        foreach (var child in ent.Comp.ChildPrototypes)
        {
            var coords = Transform(ent).Coordinates;
            var rotation = Transform(ent).LocalRotation;

            coords = coords.WithPosition(coords.Position + child.Offset);

            var childEnt = PredictedSpawnAttachedTo(child.Prototype, coords, null, rotation);
            ent.Comp.Children.Add(childEnt);
        }

        Dirty(ent);
    }

    private void OnShutdown(Entity<ChildEntitiesComponent> ent, ref ComponentShutdown args)
    {
        foreach (var child in ent.Comp.Children)
        {
            if (TerminatingOrDeleted(child))
                continue;

            PredictedQueueDel(child);
        }
    }
}
