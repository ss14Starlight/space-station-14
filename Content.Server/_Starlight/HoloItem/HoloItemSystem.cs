using Content.Shared.Examine;
using Content.Shared.PowerCell;
using Content.Shared.Interaction;
using Robust.Shared.Spawners;
using Content.Server.Hands.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.HoloItem;

public sealed class HoloItemSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HoloItemComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<HoloItemComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(EntityUid uid, HoloItemComponent component, ExaminedEvent args)
    {
        // uses the exact same process as holosigns
        var charges = _powerCell.GetRemainingUses(uid, component.ChargeUse);
        var maxCharges = _powerCell.GetMaxUses(uid, component.ChargeUse);

        using (args.PushGroup(nameof(HoloItemComponent)))
        {
            args.PushMarkup(Loc.GetString("limited-charges-charges-remaining", ("charges", charges)));

            if (charges > 0 && charges == maxCharges)
                args.PushMarkup(Loc.GetString("limited-charges-max-charges"));
        }
    }

    private void OnInteract(EntityUid uid, HoloItemComponent component, AfterInteractEvent args)
    {

        if (args.Handled
            || !args.CanReach)
            return;

        if (component.UseOnTarget)
        { 
            if(args.Target is not { } target )
                return;
            var components = StringsToRegs(component.RequiredComponents);
            foreach(var comp in components)
                if(!EntityManager.HasComponent(target, comp))
                    return;
            if(!_powerCell.TryUseCharge(uid, component.ChargeUse, user: args.User))
                return;
                                
            var holoUid = SpawnAtPosition(component.ItemPrototype, Transform(uid).Coordinates);

            EnsureComp<TimedDespawnComponent>(holoUid); //If we're trying to use the item on something it needs to have timed despawn or we risk spawning possibly infinite items.

            var ev = new AfterInteractEvent(args.User, holoUid, target, args.ClickLocation, true);
            RaiseLocalEvent(holoUid, ev);
        }
        else
        {
            if(!_powerCell.TryUseCharge(uid, component.ChargeUse, user: args.User))
                return;
            // places the holographic item at the click location.
            Spawn(component.ItemPrototype, args.ClickLocation);
        }

        args.Handled = true;
    }

    //We do it exactly like SharedCollectiveMindSystem
    private List<ComponentRegistration> StringsToRegs(List<string> input)
    {
        var list = new List<ComponentRegistration>();

        if (input == null || input.Count == 0)
            return list;

        foreach (var name in input)
        {
            var availability = _componentFactory.GetComponentAvailability(name);
            if (_componentFactory.TryGetRegistration(name, out var registration)
                && availability == ComponentAvailability.Available) list.Add(registration);
            else if (availability == ComponentAvailability.Unknown) Log.Error($"StringsToRegs failed: Unknown component name {name} passed to {nameof(HoloItemSystem)}.");
        }

        return list;
    }
}
