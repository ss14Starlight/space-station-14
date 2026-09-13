using Content.Shared.Ninja.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Content.Shared._Starlight.Ninja;
using Content.Shared._Starlight.Computers.PodConsole;

using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Shared._Starlight.Ninja;

public sealed partial class SharedPodHackerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedNinjaGlovesSystem _gloves = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PodHackerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<PodHackerComponent, ExtractDoAfterEvent>(OnDoAfter);
    }

    /// <summary>
    /// Start the doafter to hack a pod console
    /// </summary>
    private void OnBeforeInteractHand(EntityUid uid, PodHackerComponent comp, BeforeInteractHandEvent args)
    {
        if (args.Handled || !HasComp<PodConsoleComponent>(args.Target))
            return;

        // TODO: generic check event
        if (!_gloves.AbilityCheck(uid, args, out var target))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.Delay, new ExtractDoAfterEvent(), target: target, used: uid, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            CancelDuplicate = false
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    /// <summary>
    /// delete the ninja.
    /// </summary>
    private void OnDoAfter(EntityUid uid, PodHackerComponent comp, ExtractDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        // despawn ninja
        AddComp<TimedDespawnComponent>(uid);

        var ev = new PodCalledInEvent(uid, args.Target.Value);
        RaiseLocalEvent(args.User, ref ev);
    }
}

/// <summary>
/// DoAfter event for pod console extract ability.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ExtractDoAfterEvent : SimpleDoAfterEvent { }

/// <summary>
/// Raised on the user when a extract is called.
/// </summary>
/// <remarks>
[ByRefEvent]
public record struct PodCalledInEvent(EntityUid Used, EntityUid Target);
