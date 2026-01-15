// _STARLIGHT: Welder Healing System
// Handles using lit welders to repair silicon entities with automatic repeat until fully healed
// Component is placed on TARGET (IPC/borg), not the welder tool
// Listens for InteractUsingEvent when someone uses a welder on the target

using Content.Shared.Tools.Components;
using Content.Shared.Interaction;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Shared.Audio;
using Content.Shared.Damage.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Silicons;

public sealed class WelderHealingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        // Listen for when someone uses a tool on an entity with WelderHealingComponent (the target)
        SubscribeLocalEvent<WelderHealingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WelderHealingComponent, WelderHealingDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, WelderHealingComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // uid is the target (IPC with WelderHealingComponent)
        // args.Used is the tool being used (should be a welder)
        
        // Check if the tool being used is a lit welder
        if (!TryComp<WelderComponent>(args.Used, out var welder) || !welder.Enabled)
            return;

        // Check if target has damageable component
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return;

        // Check if entity uses an allowed damage container (if specified)
        if (component.AllowedContainers != null && 
            component.AllowedContainers.Count > 0 && 
            damageable.DamageContainerID != null &&
            !component.AllowedContainers.Contains(damageable.DamageContainerID))
            return;

        // Check if target needs healing
        if (damageable.TotalDamage <= 0)
            return;

        // Check fuel using solution system
        if (!_solution.TryGetSolution(args.Used, welder.FuelSolutionName, out var welderSolutionEnt, out var welderSolution))
            return;

        if (welderSolution.GetTotalPrototypeQuantity(welder.FuelReagent) < component.FuelCost)
            return;

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.Delay, new WelderHealingDoAfterEvent(), uid, target: uid, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(EntityUid uid, WelderHealingComponent component, WelderHealingDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null || args.Used == null)
            return;

        // Only run on server
        if (_net.IsClient)
            return;

        var target = args.Target.Value; // The IPC being healed
        var welder = args.Used.Value;   // The welder tool

        // Get welder component and solution
        if (!TryComp<WelderComponent>(welder, out var welderComp))
            return;

        if (!_solution.TryGetSolution(welder, welderComp.FuelSolutionName, out var welderSolutionEnt, out var welderSolution))
            return;

        // Check if we have enough fuel
        if (welderSolution.GetTotalPrototypeQuantity(welderComp.FuelReagent) < component.FuelCost)
            return;

        // Consume fuel
        _solution.RemoveReagent(welderSolutionEnt.Value, welderComp.FuelReagent, FixedPoint2.New(component.FuelCost));

        // Heal damage using configured damage specifier
        _damageable.TryChangeDamage(target, component.DamageHealed, origin: args.User);

        // Play sound and show popup
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/welder.ogg"), target);
        _popup.PopupEntity("Chassis repaired!", target, args.User);

        // Check if there's still damage to repair
        if (TryComp<DamageableComponent>(target, out var damageable) && damageable.TotalDamage > 0)
        {
            // Check if we still have enough fuel for another cycle
            if (welderSolution.GetTotalPrototypeQuantity(welderComp.FuelReagent) >= component.FuelCost)
            {
                // Start another repair cycle
                var nextDoAfterArgs = new DoAfterArgs(EntityManager, args.User, component.Delay, new WelderHealingDoAfterEvent(), uid, target: uid, used: welder)
                {
                    BreakOnMove = true,
                    BreakOnDamage = false,
                    NeedHand = true
                };

                _doAfter.TryStartDoAfter(nextDoAfterArgs);
            }
        }
    }
}
