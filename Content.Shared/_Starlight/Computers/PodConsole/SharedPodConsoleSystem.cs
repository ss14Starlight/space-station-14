using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Computers.PodConsole;

public abstract partial class SharedPodConsoleSystem : EntitySystem
{

    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PodConsoleComponent, ActivateInWorldEvent>(OnActivation);
        SubscribeLocalEvent<PodConsoleComponent, PodLaunchDoAfterEvent>(OnLaunch);
    }


    private void OnLaunch(Entity<PodConsoleComponent> ent, ref PodLaunchDoAfterEvent args)
    {
        if (args.Cancelled) return;
        ent.Comp.Locked = true;
        ent.Comp.LaunchTime = _timing.CurTime + TimeSpan.FromSeconds(10);
        _popup.PopupPredicted(Loc.GetString("pod-launching", ("time", TimeSpan.FromSeconds(10))), ent, args.User, PopupType.LargeCaution);
    }

    private void OnActivation(Entity<PodConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex) return;

        if (ent.Comp.Locked)
        {
            _popup.PopupClient(Loc.GetString("pod-locked"), ent, args.User);
            return;
        }

        args.Handled = _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User,
            TimeSpan.FromSeconds(10), new PodLaunchDoAfterEvent(), ent, ent, ent)
        {
            BreakOnDamage = true,
            BlockDuplicate = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            MovementThreshold = 1f
        });
    }
}

[Serializable, NetSerializable]
public sealed partial class PodLaunchDoAfterEvent : SimpleDoAfterEvent
{

}
