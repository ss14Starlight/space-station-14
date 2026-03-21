using Content.Shared.Interaction;
using Content.Shared._Starlight.Cybernetics.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;

namespace Content.Shared._Starlight.Cybernetics;

public sealed class CyberneticDisruptorSystem : EntitySystem
{
    [Dependency] private readonly SharedCyberneticDisruptionSystem _disrupt = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyberneticDisruptorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CyberneticDisruptorComponent, CyberneticDisruptorDoafterEvent>(OnDoafter);
    }
    private void OnAfterInteract(EntityUid uid, CyberneticDisruptorComponent comp, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target)
            return;
        
        if (!TryComp(target, out HumanoidAppearanceComponent? _))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, comp.UseTime, new CyberneticDisruptorDoafterEvent(), uid, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            RequireCanInteract = true,
            CancelDuplicate = true
        };
        _audio.PlayPredicted(comp.SoundStart, args.User, args.User);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoafter(EntityUid uid, CyberneticDisruptorComponent comp, CyberneticDisruptorDoafterEvent args)
    { 
        if (args.Target is not { } target)
            return;

        if(args.Cancelled)
            return;

        _disrupt.TryAddCyberneticDisruptionDuration(target, comp.Duration, comp.RefreshDuration);
        _audio.PlayPredicted(comp.SoundFinish, args.User, args.User);
        args.Handled = true;
    }
}
