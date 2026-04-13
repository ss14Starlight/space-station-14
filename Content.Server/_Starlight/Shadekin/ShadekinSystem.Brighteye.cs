using Content.Shared._Starlight.Shadekin;
using Content.Shared.Humanoid;
using Content.Shared.Rejuvenate;
using Content.Shared.Popups;
using Content.Shared.Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Mobs;
using Content.Shared.Inventory;
using Content.Server.Spawners.Components;
using Content.Server._Starlight.Bluespace;
using Content.Shared.Zombies;
using Content.Server.Cargo.Components;
using Content.Shared.Mindshield.Components;

namespace Content.Server._Starlight.Shadekin;

public sealed partial class ShadekinSystem : EntitySystem
{
    public void InitializeBrighteye()
    {
        SubscribeLocalEvent<BrighteyeComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<BrighteyeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BrighteyeComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<BrighteyeComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BrighteyeComponent, NullSpaceShuntEvent>(NullSpaceShunt);
        SubscribeLocalEvent<BrighteyeComponent, EntityZombifiedEvent>((uid, _, _) => RemComp<BrighteyeComponent>(uid));
        SubscribeLocalEvent<MindShieldComponent, ComponentStartup>(MindShieldImplanted);
        SubscribeLocalEvent<BrighteyeComponent, ForcedPrototypeDoSpecialEvent>(ForcedPrototypeDoSpecial);

        SubscribeLocalEvent<OrganShadekinCoreComponent, SurgeryOrganImplantationCompleted>(OnCoreOrganImplanted);
        SubscribeLocalEvent<OrganShadekinCoreComponent, SurgeryOrganExtracted>(OnCoreOrganExtracted);
    }

    private void OnInit(EntityUid uid, BrighteyeComponent component, ComponentStartup args)
    {
        if (!HasComp<ShadekinComponent>(uid))
        {
            RemComp<BrighteyeComponent>(uid);
            return;
        }

        RemCompDeferred<MindShieldComponent>(uid);

        _alerts.ShowAlert(uid, component.BrighteyeAlert);
        _alerts.ShowAlert(uid, component.PortalAlert);

        _actionsSystem.AddAction(uid, ref component.PortalAction, component.BrighteyePortalAction, uid);
        _actionsSystem.AddAction(uid, ref component.PhaseAction, component.BrighteyePhaseAction, uid);
        _actionsSystem.AddAction(uid, ref component.ShadeSkipAction, component.BrighteyeShadeSkipAction, uid);
        _actionsSystem.AddAction(uid, ref component.CreateShadeAction, component.BrighteyeCreateShadeAction, uid);
        _actionsSystem.AddAction(uid, ref component.DarkTrapAction, component.BrighteyeDarkTrapAction, uid);

        if (TryComp<BodyComponent>(uid, out var body))
            foreach (var core in _bodySystem.GetBodyOrganEntityComps<OrganShadekinCoreComponent>((uid, body)))
            {
                core.Comp1.Damaged = false;

                _tag.AddTag(core, _coreTag);
                _tag.RemoveTag(core, _damagedCoreTag);

                if (EnsureComp<StaticPriceComponent>(core, out var price))
                    price.Price = core.Comp1.UndmagedPrice;

                if (core.Comp1.OrganOwner != uid)
                {
                    component.MaxEnergy = 100;
                    component.PhaseCost = 100;

                    _alerts.ClearAlert(uid, component.PortalAlert);
                    _actionsSystem.RemoveAction(uid, component.PortalAction);
                    _actionsSystem.RemoveAction(uid, component.ShadeSkipAction);
                    _actionsSystem.RemoveAction(uid, component.DarkTrapAction);
                }
            }

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            SetBrighteyes(uid, humanoid);
    }

    private void ForcedPrototypeDoSpecial(EntityUid uid, BrighteyeComponent component, ForcedPrototypeDoSpecialEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            SetBrighteyes(uid, humanoid);

        if (TryComp<BodyComponent>(uid, out var body))
            foreach (var core in _bodySystem.GetBodyOrganEntityComps<OrganShadekinCoreComponent>((uid, body)))
            {
                core.Comp1.Damaged = false;
                _tag.AddTag(core, _coreTag);
                _tag.RemoveTag(core, _damagedCoreTag);

                if (EnsureComp<StaticPriceComponent>(core, out var price))
                    price.Price = core.Comp1.UndmagedPrice;
            }

        component.PortalNeedStation = false;

        RemCompDeferred<MindShieldComponent>(uid);
    }

    // ! Gosh this is bad... But there no event to get implanted shit? or mindshield? il do this for now change if need later!
    private void MindShieldImplanted(EntityUid uid, MindShieldComponent comp, ComponentStartup args)
    {
        if (HasComp<BrighteyeComponent>(uid))
        {
            RemCompDeferred<MindShieldComponent>(uid);
            return;
        }
    }

    private void OnCoreOrganImplanted(Entity<OrganShadekinCoreComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!ent.Comp.Damaged)
            EnsureComp<BrighteyeComponent>(args.Body);
    }

    private void OnCoreOrganExtracted(Entity<OrganShadekinCoreComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (HasComp<BrighteyeComponent>(args.Body) && !ent.Comp.Damaged)
            RemComp<BrighteyeComponent>(args.Body);
    }

    private void OnShutdown(EntityUid uid, BrighteyeComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, component.BrighteyeAlert);
        _alerts.ClearAlert(uid, component.PortalAlert);
        _alerts.ClearAlert(uid, component.RejuvenationAlert);

        _actionsSystem.RemoveAction(uid, component.PortalAction);
        _actionsSystem.RemoveAction(uid, component.PhaseAction);
        _actionsSystem.RemoveAction(uid, component.ShadeSkipAction);
        _actionsSystem.RemoveAction(uid, component.CreateShadeAction);
        _actionsSystem.RemoveAction(uid, component.DarkTrapAction);

        if (component.Portal is not null)
        {
            SpawnAtPosition(component.ShadekinShadow, Transform(component.Portal.Value).Coordinates);
            QueueDel(component.Portal.Value);
        }

        if (TryComp<BodyComponent>(uid, out var body))
            foreach (var core in _bodySystem.GetBodyOrganEntityComps<OrganShadekinCoreComponent>((uid, body)))
            {
                core.Comp1.Damaged = true;

                _tag.AddTag(core, _damagedCoreTag);
                _tag.RemoveTag(core, _coreTag);

                if (EnsureComp<StaticPriceComponent>(core, out var price))
                    price.Price = core.Comp1.DmagedPrice;
            }

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            SetBlackeyes(uid, humanoid);
    }

    private void OnRejuvenate(EntityUid uid, BrighteyeComponent component, RejuvenateEvent args)
    {
        component.Energy = component.MaxEnergy;
        Dirty(uid, component);
    }

    private void NullSpaceShunt(EntityUid uid, BrighteyeComponent component, NullSpaceShuntEvent args)
    {
        component.Energy = 0;
        Dirty(uid, component);
    }

    private void OnMobStateChanged(EntityUid uid, BrighteyeComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        // We hit Crit/Death we lose energy... EVERYTIME!
        component.Energy = 0;
        Dirty(uid, component);

        // Make shit modular! (Aka for future devs, this can be used to block Rejuvenation)
        var ev = new OnBrighteyeRejuvenateAttemptEvent(uid);
        RaiseLocalEvent(uid, ev);

        if (ev.Cancelled)
            return;

        // ZombifyOnDeath? Yeah no Regen for you buddy!
        if (HasComp<ZombifyOnDeathComponent>(uid))
            return;

        // Do we have a portal? if no... WE DIE!
        if (component.Portal is null && !AreWeInTheDark(uid))
            return;

        // Get a valid Location to get TP at.
        var spawns = new List<EntityUid>();
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var spawnUid, out _, out var xform))
            if (_mapSystem.TryGetMap(xform.MapID, out var spawnmap))
                if (_tag.HasTag(spawnmap.Value, _theDarkTag))
                    spawns.Add(spawnUid);

        // If no valid spawnpoint... we just... DIE!
        if (spawns.Count <= 0)
            return;

        _random.Shuffle(spawns);

        // First, Drop Everything we have.
        if (TryComp<InventoryComponent>(uid, out var inventoryComponent) && _inventorySystem.TryGetSlots(uid, out var slots))
            foreach (var slot in slots)
                _inventorySystem.TryUnequip(uid, slot.Name, true, true, false, inventoryComponent);

        // Spawn the Shadow.
        SpawnAtPosition(component.ShadekinShadow, Transform(uid).Coordinates);

        // Teleport to "The Dark"
        foreach (var spawnUid in spawns)
        {
            _transform.SetCoordinates(uid, Transform(spawnUid).Coordinates);
            break;
        }

        var effect = SpawnAtPosition(component.ShadekinPhaseInEffect2, Transform(uid).Coordinates);
        Transform(effect).LocalRotation = Transform(uid).LocalRotation;

        RaiseLocalEvent(uid, new RejuvenateEvent());

        component.Energy = 0;
        component.Rejuvenating = true;
        _alerts.ShowAlert(uid, component.RejuvenationAlert);
        Dirty(uid, component);
    }

    /// <summary>
    /// Change the humanoid eye to be bright and glow!
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="humanoid"></param>
    public void SetBrighteyes(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        humanoid.EyeColor = EyeColor.MakeBrighteyeValid(humanoid.EyeColor);
        humanoid.EyeGlowing = true;
        Dirty(uid, humanoid);
    }

    /// <summary>
    /// Change the humanoid eye to be validated by HumanoidEyeColor.Shadekin (Blackeyes)
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="humanoid"></param>
    public void SetBlackeyes(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        humanoid.EyeColor = EyeColor.MakeShadekinValid(humanoid.EyeColor);
        humanoid.EyeGlowing = false;

        Dirty(uid, humanoid);
    }

    /// <summary>
    /// When triggered, will check if we have enough energy and if yes drain the energy and return the value.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="cost">cost of energy (if null then no cost needed)</param>
    /// <returns></returns>
    public bool OnAttemptEnergyUse(EntityUid uid, BrighteyeComponent component, int? cost = null)
    {
        var ev = new OnAttemptEnergyUseEvent(uid);
        RaiseLocalEvent(uid, ev);

        if (ev.Cancelled)
            return false;

        if (cost is null)
            return true;

        if (component.Energy >= cost)
        {
            component.Energy -= (int)cost;
            Dirty(uid, component);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("shadekin-noenergy"), uid, uid, PopupType.LargeCaution);
            return false;
        }

        return true;
    }

    private void UpdateEnergy(EntityUid uid, ShadekinComponent component, BrighteyeComponent brighteye)
    {
        if (brighteye.Rejuvenating && brighteye.Energy >= brighteye.MaxEnergy)
        {
            brighteye.Rejuvenating = false;
            _popup.PopupEntity(Loc.GetString("shadekin-rejuvenate-compleated"), uid, uid, PopupType.LargeCaution);
            _alerts.ClearAlert(uid, brighteye.RejuvenationAlert);
        }

        if (component.CurrentState == ShadekinState.Low) // On Low State, we gain and lose nothing!
            return;

        int newenergy = 0;

        if (brighteye.Energy > 0 && component.CurrentState != ShadekinState.Dark) // First we will handle energy drain on light.
        {
            if (component.CurrentState == ShadekinState.Extreme)
                newenergy = -5;
            else if (component.CurrentState == ShadekinState.High)
                newenergy = -2;
            else if (component.CurrentState == ShadekinState.Annoying)
                newenergy = -1;
        }
        else if (brighteye.Energy < brighteye.MaxEnergy && component.CurrentState == ShadekinState.Dark) // We now handle energy gain.
        {
            // TODO: Add buffs here depanding on different situations?
            newenergy = 1;
        }

        brighteye.Energy = Math.Clamp(brighteye.Energy + newenergy, 0, brighteye.MaxEnergy);
        Dirty(uid, brighteye);
    }
}
