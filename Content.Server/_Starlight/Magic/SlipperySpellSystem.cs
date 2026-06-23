using Content.Server.DoAfter;
using Content.Server.Forensics;
using Content.Server.Popups;
using Content.Shared._Starlight.Janitorial;
using Content.Shared._Starlight.Magic;
using Content.Shared._Starlight.Magic.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.StepTrigger.Components;

namespace Content.Server._Starlight.Magic;

/// <summary>
/// Handles removing the wizard's Slippery Slope spell effect when soap is used on the affected entity.
/// </summary>
public sealed class SlipperySpellSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // AfterInteractEvent fires on the used item.
        // SoapComponent is a marker on all soap bar entities.
        SubscribeLocalEvent<SoapComponent, AfterInteractEvent>(OnSoapUsed);
        SubscribeLocalEvent<SoapComponent, CleanSlipperySpellDoAfterEvent>(OnDoAfter);
    }

    private void OnSoapUsed(Entity<SoapComponent> soap, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        var target = args.Target.Value;

        if (!HasComp<SpellSlipperyComponent>(target))
            return;

        var cleanDelay = TryComp<CleansForensicsComponent>(soap, out var cleansForensics) ? cleansForensics.CleanDelay : 12f;
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, cleanDelay, new CleanSlipperySpellDoAfterEvent(), soap, target: target, used: soap)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.3f,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupEntity(Loc.GetString("spell-slippery-clean-start", ("target", target)), args.User, args.User, PopupType.Small);
            args.Handled = true;
        }
    }

    private void OnDoAfter(Entity<SoapComponent> soap, ref CleanSlipperySpellDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        var target = args.Target.Value;

        if (!HasComp<SpellSlipperyComponent>(target))
            return;

        RemComp<SpellSlipperyComponent>(target);
        RemComp<SlipperyComponent>(target);
        RemComp<StepTriggerComponent>(target);

        _popup.PopupEntity(Loc.GetString("spell-slippery-clean-success", ("target", target)), args.User, args.User, PopupType.Medium);
    }
}
