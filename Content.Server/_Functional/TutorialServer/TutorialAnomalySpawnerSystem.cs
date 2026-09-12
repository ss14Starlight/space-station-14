using Content.Shared._Functional.TutorialServer;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Spawns a fixed tutorial anomaly when a player activates the spawn pad.
/// </summary>
public sealed partial class TutorialAnomalySpawnerSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TutorialAnomalySpawnerComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(Entity<TutorialAnomalySpawnerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Spawned)
        {
            _popup.PopupEntity(Loc.GetString("tutorial-anomaly-spawner-already"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.SpawnedAnomaly is { } existing && Exists(existing) && !TerminatingOrDeleted(existing))
        {
            ent.Comp.Spawned = true;
            Dirty(ent);
            args.Handled = true;
            return;
        }

        var coords = Transform(ent).Coordinates.Offset(ent.Comp.Offset);
        var anomaly = Spawn(ent.Comp.AnomalyProto, coords);
        ent.Comp.SpawnedAnomaly = anomaly;
        ent.Comp.Spawned = true;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("tutorial-anomaly-spawner-spawned"), ent, args.User);
        args.Handled = true;
    }
}
