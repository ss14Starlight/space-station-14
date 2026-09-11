using Content.Shared.Humanoid;
using Content.Shared.Rejuvenate;
using Content.Shared.Popups;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Components;
using Content.Shared.Mobs;
using Content.Shared.Inventory;
using Content.Shared.Zombies;
using Content.Shared._Starlight.Bluespace;
using Content.Shared.Mindshield.Components;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared._Starlight.Station;
using Content.Shared.Cargo.Components;
using Content.Shared.Spawners.Components;

namespace Content.Shared._Starlight.Shadekin;

public sealed partial class ShadekinSystem
{
    [SubscribeLocalEvent]
    private void OnZombify(Entity<BrighteyeComponent> ent, ref EntityZombifiedEvent args)
        => RemComp<BrighteyeComponent>(ent.Owner);

    [SubscribeLocalEvent]
    private void OnInit(Entity<BrighteyeComponent> ent, ref ComponentStartup _)
    {
        if (!HasComp<ShadekinComponent>(ent.Owner))
        {
            RemComp<BrighteyeComponent>(ent.Owner);
            return;
        }

        RemCompDeferred<MindShieldComponent>(ent.Owner);

        _language.AddLanguage(ent.Owner, "Empathy");

        _alerts.ShowAlert(ent.Owner, ent.Comp.BrighteyeAlert);
        _alerts.ShowAlert(ent.Owner, ent.Comp.PortalAlert);

        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.PortalAction, ent.Comp.BrighteyePortalAction, ent.Owner);
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.PhaseAction, ent.Comp.BrighteyePhaseAction, ent.Owner);
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.ShadeSkipAction, ent.Comp.BrighteyeShadeSkipAction, ent.Owner);
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.CreateShadeAction, ent.Comp.BrighteyeCreateShadeAction, ent.Owner);
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.DarkTrapAction, ent.Comp.BrighteyeDarkTrapAction, ent.Owner);

        if (TryComp<BodyComponent>(ent.Owner, out var body))
            foreach (var core in _bodySystem.GetBodyOrganEntityComps<OrganShadekinCoreComponent>((ent.Owner, body)))
            {
                core.Comp1.Damaged = false;

                _tag.AddTag(core, _coreTag);
                _tag.RemoveTag(core, _damagedCoreTag);

                if (EnsureComp<StaticPriceComponent>(core, out var price))
                    price.Price = core.Comp1.UndmagedPrice;

                if (core.Comp1.OrganOwner != ent.Owner)
                {
                    ent.Comp.LesserKin = true;
                    ent.Comp.MaxEnergy = 100;
                    ent.Comp.PhaseCost = 100;

                    _alerts.ClearAlert(ent.Owner, ent.Comp.PortalAlert);
                    _actionsSystem.RemoveAction(ent.Owner, ent.Comp.PortalAction);
                    _actionsSystem.RemoveAction(ent.Owner, ent.Comp.ShadeSkipAction);
                    _actionsSystem.RemoveAction(ent.Owner, ent.Comp.DarkTrapAction);
                }
            }

        if (TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            SetBrighteyes(ent.Owner, humanoid);
    }

    [SubscribeLocalEvent]
    private void ForcedPrototypeDoSpecial(Entity<BrighteyeComponent> ent, ref ForcedPrototypeDoSpecialEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            SetBrighteyes(ent.Owner, humanoid);

        if (TryComp<BodyComponent>(ent.Owner, out var body))
            foreach (var core in _bodySystem.GetBodyOrganEntityComps<OrganShadekinCoreComponent>((ent.Owner, body)))
            {
                core.Comp1.Damaged = false;
                _tag.AddTag(core, _coreTag);
                _tag.RemoveTag(core, _damagedCoreTag);

                if (EnsureComp<StaticPriceComponent>(core, out var price))
                    price.Price = core.Comp1.UndmagedPrice;
            }

        ent.Comp.PortalNeedStation = false;

        RemCompDeferred<MindShieldComponent>(ent.Owner);
    }

    // ! Gosh this is bad... But there no event to get implanted shit? or mindshield? il do this for now change if need later!
    [SubscribeLocalEvent]
    private void MindShieldImplanted(EntityUid uid, MindShieldComponent comp, ComponentStartup args)
    {
        if (HasComp<BrighteyeComponent>(uid))
            RemCompDeferred<MindShieldComponent>(uid);
    }

    [SubscribeLocalEvent]
    private void OnCoreOrganImplanted(Entity<OrganShadekinCoreComponent> ent, ref SurgeryOrganImplantationCompleted args)
    {
        if (!ent.Comp.Damaged)
            EnsureComp<BrighteyeComponent>(args.Body);
    }

    [SubscribeLocalEvent]
    private void OnCoreOrganExtracted(Entity<OrganShadekinCoreComponent> ent, ref SurgeryOrganExtracted args)
    {
        if (HasComp<BrighteyeComponent>(args.Body) && !ent.Comp.Damaged)
            RemComp<BrighteyeComponent>(args.Body);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<BrighteyeComponent> ent, ref ComponentShutdown _)
    {
        _language.RemoveLanguage(ent.Owner, "Empathy", removeUnderstood: false);

        _alerts.ClearAlert(ent.Owner, ent.Comp.BrighteyeAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.PortalAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.RejuvenationAlert);

        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.PortalAction);
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.PhaseAction);
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.ShadeSkipAction);
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.CreateShadeAction);
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.DarkTrapAction);

        if (ent.Comp.Portal is not null)
        {
            PredictedSpawnAtPosition(ent.Comp.ShadekinShadow, Transform(ent.Comp.Portal.Value).Coordinates);
            PredictedQueueDel(ent.Comp.Portal.Value);
        }

        if (TryComp<BodyComponent>(ent.Owner, out var body))
            foreach (var core in _bodySystem.GetBodyOrganEntityComps<OrganShadekinCoreComponent>((ent.Owner, body)))
            {
                core.Comp1.Damaged = true;

                _tag.AddTag(core, _damagedCoreTag);
                _tag.RemoveTag(core, _coreTag);

                if (EnsureComp<StaticPriceComponent>(core, out var price))
                    price.Price = core.Comp1.DmagedPrice;
            }

        if (TryComp<HumanoidAppearanceComponent>(ent.Owner, out var humanoid))
            SetBlackeyes(ent.Owner, humanoid);
    }

    [SubscribeLocalEvent]
    private void OnRejuvenate(Entity<BrighteyeComponent> ent, ref RejuvenateEvent _)
    {
        ent.Comp.Energy = ent.Comp.MaxEnergy;
        Dirty(ent.Owner, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void NullSpaceShunt(Entity<BrighteyeComponent> ent, ref NullSpaceShuntEvent _)
    {
        ent.Comp.Energy = 0;
        Dirty(ent.Owner, ent.Comp);
    }

    [SubscribeLocalEvent]
    private void OnMobStateChanged(Entity<BrighteyeComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        // We hit Crit/Death we lose energy... EVERYTIME!
        ent.Comp.Energy = 0;
        Dirty(ent.Owner, ent.Comp);

        // Make shit modular! (Aka for future devs, this can be used to block Rejuvenation)
        var ev = new OnBrighteyeRejuvenateAttemptEvent(ent.Owner);
        RaiseLocalEvent(ent.Owner, ev);

        if (ev.Cancelled)
            return;

        // ZombifyOnDeath? Yeah no Regen for you buddy!
        if (HasComp<ZombifyOnDeathComponent>(ent.Owner))
            return;

        // Do we have a portal? if no... WE DIE!
        if (ent.Comp.Portal is null && !AreWeInTheDark(ent.Owner))
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
        if (TryComp<InventoryComponent>(ent.Owner, out var inventoryComponent) && _inventorySystem.TryGetSlots(ent.Owner, out var slots))
            foreach (var slot in slots)
                _inventorySystem.TryUnequip(ent.Owner, slot.Name, true, true, false, inventoryComponent);

        // Spawn the Shadow.
        PredictedSpawnAtPosition(ent.Comp.ShadekinShadow, Transform(ent.Owner).Coordinates);

        // Teleport to "The Dark"
        foreach (var spawnUid in spawns)
        {
            _transform.SetCoordinates(ent.Owner, Transform(spawnUid).Coordinates);
            break;
        }

        var effect = PredictedSpawnAtPosition(ent.Comp.ShadekinPhaseInEffect2, Transform(ent.Owner).Coordinates);
        Transform(effect).LocalRotation = Transform(ent.Owner).LocalRotation;

        RaiseLocalEvent(ent.Owner, new RejuvenateEvent());

        ent.Comp.Energy = 0;
        ent.Comp.Rejuvenating = true;
        _alerts.ShowAlert(ent.Owner, ent.Comp.RejuvenationAlert);
        Dirty(ent.Owner, ent.Comp);
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

    public void SetBlackeyes(EntityUid uid, HumanoidAppearanceComponent humanoid)
    {
        humanoid.EyeColor = EyeColor.MakeShadekinValid(humanoid.EyeColor);
        humanoid.EyeGlowing = false;

        Dirty(uid, humanoid);
    }

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
            _popup.PopupClient(Loc.GetString("shadekin-noenergy"), uid, uid, PopupType.LargeCaution);
            return false;
        }

        return true;
    }

    private void UpdateEnergy(EntityUid uid, ShadekinComponent component, BrighteyeComponent brighteye)
    {
        if (brighteye.Rejuvenating && brighteye.Energy >= brighteye.MaxEnergy)
        {
            brighteye.Rejuvenating = false;
            Dirty(uid, brighteye);
            _popup.PopupClient(Loc.GetString("shadekin-rejuvenate-compleated"), uid, uid, PopupType.LargeCaution);
            _alerts.ClearAlert(uid, brighteye.RejuvenationAlert);
        }

        if (component.CurrentState == ShadekinState.Low) // On Low State, we gain and lose nothing!
            return;

        var newEnergy = 0;

        if (brighteye.Energy > 0 && component.CurrentState != ShadekinState.Dark) // First we will handle energy drain on light.
        {
            if (component.CurrentState == ShadekinState.Extreme)
                newEnergy = -5;
            else if (component.CurrentState == ShadekinState.High)
                newEnergy = -2;
            else if (component.CurrentState == ShadekinState.Annoying)
                newEnergy = -1;
        }
        else if (brighteye.Energy < brighteye.MaxEnergy && component.CurrentState == ShadekinState.Dark) // We now handle energy gain.
        {
            // TODO: Add buffs here depanding on different situations?
            newEnergy = 1;
        }

        var energy = Math.Clamp(brighteye.Energy + newEnergy, 0, brighteye.MaxEnergy);
        if (energy == brighteye.Energy)
            return;

        brighteye.Energy = energy;
        Dirty(uid, brighteye);
    }
}
