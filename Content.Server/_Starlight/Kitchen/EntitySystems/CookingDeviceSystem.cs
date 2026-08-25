using Content.Server.Administration.Logs;
using Content.Server.Construction;
using Content.Server.Explosion.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Database;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Random;
using Robust.Shared.Audio;
using Content.Server.Lightning;
using Content.Shared.Item;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Stacks;
using Content.Server.Construction.Components;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Server._Starlight.Kitchen.Components;

// ReSharper disable once CheckNamespace
namespace Content.Server.Kitchen.EntitySystems;

public sealed partial class CookingDeviceSystem : EntitySystem
{
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private RecipeManager _recipeManager = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private TemperatureSystem _temperature = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private HandsSystem _handsSystem = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private SharedSuicideSystem _suicide = default!;
    [Dependency] private SharedPowerStateSystem _powerState = default!;

    private static readonly EntProtoId _malfunctionSpark = "Spark";

    private static readonly ProtoId<TagPrototype> _metalTag = "Metal";
    private static readonly ProtoId<TagPrototype> _plasticTag = "Plastic";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CookingDeviceComponent, EntInsertedIntoContainerMessage>(OnContentUpdate);
        SubscribeLocalEvent<CookingDeviceComponent, EntRemovedFromContainerMessage>(OnContentUpdate);
        SubscribeLocalEvent<CookingDeviceComponent, InteractUsingEvent>(OnInteractUsing, after: new[] { typeof(AnchorableSystem) });
        SubscribeLocalEvent<CookingDeviceComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CookingDeviceComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<CookingDeviceComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<CookingDeviceComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CookingDeviceComponent, SuicideByEnvironmentEvent>(OnSuicideByEnvironment);

        SubscribeLocalEvent<CookingDeviceComponent, SignalReceivedEvent>(OnSignalReceived);

        SubscribeLocalEvent<CookingDeviceComponent, MicrowaveStartCookMessage>((ent, ref ev) => Wzhzhzh(ent, ev.Actor));
        SubscribeLocalEvent<CookingDeviceComponent, MicrowaveStopCookMessage>(OnStopMessage);
        SubscribeLocalEvent<CookingDeviceComponent, MicrowaveEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<CookingDeviceComponent, MicrowaveEjectSolidIndexedMessage>(OnEjectIndex);
        SubscribeLocalEvent<CookingDeviceComponent, MicrowaveSelectCookTimeMessage>(OnSelectTime);

        SubscribeLocalEvent<ActiveCookingDeviceComponent, ComponentStartup>(OnCookStart);
        SubscribeLocalEvent<ActiveCookingDeviceComponent, ComponentShutdown>(OnCookStop);
        SubscribeLocalEvent<ActiveCookingDeviceComponent, EntInsertedIntoContainerMessage>(OnActiveMicrowaveInsert);
        SubscribeLocalEvent<ActiveCookingDeviceComponent, EntRemovedFromContainerMessage>(OnActiveMicrowaveRemove);

        SubscribeLocalEvent<ActivelyCookedComponent, OnConstructionTemperatureEvent>(OnConstructionTemp);
        SubscribeLocalEvent<ActivelyCookedComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnReactionAttempt);

        SubscribeLocalEvent<FoodRecipeProviderComponent, GetSecretRecipesEvent>(OnGetSecretRecipes);

    }

    [SubscribeLocalEvent]
    private void OnBuiOpened(EntityUid uid, CookingDeviceComponent component, BoundUIOpenedEvent args) => SetAppearance(uid, null, component, Opened: true);

    [SubscribeLocalEvent]
    private void OnBuiClosed(EntityUid uid, CookingDeviceComponent component, BoundUIClosedEvent args) => SetAppearance(uid, null, component, Opened: false);

    private void OnCookStart(Entity<ActiveCookingDeviceComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<CookingDeviceComponent>(ent, out var CookingDeviceComponent))
            return;
        SetAppearance(ent.Owner, MicrowaveVisualState.Cooking, CookingDeviceComponent);

        CookingDeviceComponent.PlayingStream = _audio.PlayPvs(CookingDeviceComponent.LoopingSound, ent, AudioParams.Default.WithLoop(true).WithMaxDistance(5))?.Entity;
        _powerState.SetWorkingState(ent.Owner, true);
    }

    private void OnCookStop(Entity<ActiveCookingDeviceComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<CookingDeviceComponent>(ent, out var CookingDeviceComponent))
            return;

        SetAppearance(ent.Owner, MicrowaveVisualState.Idle, CookingDeviceComponent);
        CookingDeviceComponent.PlayingStream = _audio.Stop(CookingDeviceComponent.PlayingStream);
        CookingDeviceComponent.StartedCookTime = TimeSpan.Zero;
        UpdateUserInterfaceState((ent.Owner, CookingDeviceComponent), false);
        _powerState.SetWorkingState(ent.Owner, false);
    }

    private void OnActiveMicrowaveInsert(Entity<ActiveCookingDeviceComponent> ent, ref EntInsertedIntoContainerMessage args)
        => AddComp<ActivelyCookedComponent>(args.Entity).Microwave = ent.Owner;

    private void OnActiveMicrowaveRemove(Entity<ActiveCookingDeviceComponent> ent, ref EntRemovedFromContainerMessage args)
        => RemCompDeferred<ActivelyCookedComponent>(args.Entity);

    // Stop items from transforming through constructiongraphs while being microwaved.
    // They might be reserved for a microwave recipe.
    private void OnConstructionTemp(Entity<ActivelyCookedComponent> ent, ref OnConstructionTemperatureEvent args) => args.Result = HandleResult.False;

    // Stop reagents from reacting if they are currently reserved for a microwave recipe.
    // For example Egg would cook into EggCooked, causing it to not being removed once we are done microwaving.
    private void OnReactionAttempt(Entity<ActivelyCookedComponent> ent, ref SolutionRelayEvent<ReactionAttemptEvent> args)
    {
        if (!TryComp<ActiveCookingDeviceComponent>(ent.Comp.Microwave, out var activeMicrowaveComp))
            return;

        if (activeMicrowaveComp.PortionedRecipes.Count == 0)
            return;

        foreach (var (recipe, availableAmount) in activeMicrowaveComp.PortionedRecipes)
        {
            var recipeReagents = recipe.IngredientsReagents.Keys;

            foreach (var reagent in recipeReagents)
            {
                if (args.Event.Reaction.Reactants.ContainsKey(reagent))
                {
                    args.Event.Cancelled = true;
                    return;
                }
            }
        }
    }

    /// <summary>
    ///     Adds temperature to every item in the microwave,
    ///     based on the time it took to microwave.
    /// </summary>
    /// <param name="component">The microwave that is heating up.</param>
    /// <param name="time">The time on the microwave, in seconds.</param>
    private void AddTemperature(CookingDeviceComponent component, float time)
    {
        var heatToAdd = time * component.BaseHeatMultiplier;
        foreach (var entity in component.Storage.ContainedEntities)
        {
            if (TryComp<TemperatureComponent>(entity, out var tempComp))
                _temperature.ChangeHeat(entity, heatToAdd * component.ObjectHeatMultiplier, false, tempComp);

            if (!TryComp<SolutionContainerManagerComponent>(entity, out var solutions))
                continue;
            foreach (var (_, soln) in _solutionContainer.EnumerateSolutions((entity, solutions)))
            {
                var solution = soln.Comp.Solution;
                if (solution.Temperature > component.TemperatureUpperThreshold)
                    continue;

                _solutionContainer.AddThermalEnergy(soln, heatToAdd);
            }
        }
    }

    private bool SubtractContents(CookingDeviceComponent component, FoodRecipePrototype recipe)
    {
        // TODO Turn recipe.IngredientsReagents into a ReagentQuantity[]

        var totalReagentsToRemove = new Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>(recipe.IngredientsReagents);

        foreach (var (reagent, required) in recipe.IngredientsReagents)
        {
            var available = FixedPoint2.Zero;

            foreach (var item in component.Storage.ContainedEntities)
            {
                if (!_solutionContainer.TryGetDrainableSolution(item, out _, out var solution))
                    continue;

                available += solution.GetTotalPrototypeQuantity(reagent);
            }

            if (available < required)
                return false;
        }

        foreach (var recipeSolid in recipe.IngredientsSolids)
        {
            var available = 0;

            foreach (var item in component.Storage.ContainedEntities)
            {
                string? itemID = null;

                if (TryComp<StackComponent>(item, out var stackComp))
                    itemID = _prototype.Index<StackPrototype>(stackComp.StackTypeId).Spawn;
                else
                {
                    var metaData = MetaData(item);
                    if (metaData.EntityPrototype == null)
                        continue;
                    itemID = metaData.EntityPrototype.ID;
                }

                if (itemID == recipeSolid.Key)
                {
                    available += stackComp?.Count ?? 1;
                }
            }

            if (available < recipeSolid.Value)
                return false;
        }

        // this is spaghetti ngl
        foreach (var item in component.Storage.ContainedEntities)
        {
            // use the same reagents as when we selected the recipe
            if (!_solutionContainer.TryGetDrainableSolution(item, out var solutionEntity, out var solution))
                continue;

            foreach (var (reagent, _) in recipe.IngredientsReagents)
            {
                // removed everything
                if (!totalReagentsToRemove.ContainsKey(reagent))
                    continue;

                var quant = solution.GetTotalPrototypeQuantity(reagent);

                if (quant >= totalReagentsToRemove[reagent])
                {
                    quant = totalReagentsToRemove[reagent];
                    totalReagentsToRemove.Remove(reagent);
                }
                else
                    totalReagentsToRemove[reagent] -= quant;

                _solutionContainer.RemoveReagent(solutionEntity.Value, reagent, quant);
            }
        }

        foreach (var recipeSolid in recipe.IngredientsSolids)
        {
            for (var i = 0; i < recipeSolid.Value; i++)
            {
                foreach (var item in component.Storage.ContainedEntities)
                {
                    string? itemID = null;

                    // If an entity has a stack component, use the stacktype instead of prototype id
                    if (TryComp<StackComponent>(item, out var stackComp))
                    {
                        itemID = _prototype.Index(stackComp.StackTypeId).Spawn;
                    }
                    else
                    {
                        var metaData = MetaData(item);
                        if (metaData.EntityPrototype == null)
                            continue;
                        itemID = metaData.EntityPrototype.ID;
                    }

                    if (itemID != recipeSolid.Key)
                        continue;

                    if (stackComp is not null)
                    {
                        if (stackComp.Count == 1) {
                            _container.Remove(item, component.Storage);
                        }
                        _stack.ReduceCount((item, stackComp), 1);
                        break;
                    }
                    else
                    {
                        _container.Remove(item, component.Storage);
                        Del(item);
                        break;
                    }
                }
            }
        }

        return true;
    }

    [SubscribeLocalEvent]
    private void OnInit(Entity<CookingDeviceComponent> ent, ref ComponentInit args) => ent.Comp.Storage = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<CookingDeviceComponent> ent, ref MapInitEvent args) => _deviceLink.EnsureSinkPorts(ent, ent.Comp.OnPort);

    /// <summary>
    /// Kills the user by microwaving their head
    /// TODO: Make this not awful, it keeps any items attached to your head still on and you can revive someone and cogni them so you have some dumb headless fuck running around. I've seen it happen.
    /// </summary>
    private void OnSuicideByEnvironment(Entity<CookingDeviceComponent> ent, ref SuicideByEnvironmentEvent args)
    {
        if (args.Handled)
            return;

        // The act of getting your head microwaved doesn't actually kill you
        if (!TryComp<DamageableComponent>(args.Victim, out var damageableComponent))
            return;

        // The application of lethal damage is what kills you...
        _suicide.ApplyLethalDamage((args.Victim, damageableComponent), "Heat");

        var victim = args.Victim;

        var othersMessage = Loc.GetString("microwave-component-suicide-others-message", ("victim", victim));
        var selfMessage = Loc.GetString("microwave-component-suicide-message");

        _popupSystem.PopupEntity(othersMessage, victim, Filter.PvsExcept(victim), true);
        _popupSystem.PopupEntity(selfMessage, victim, victim);

        _audio.PlayPvs(ent.Comp.ClickSound, ent.Owner, AudioParams.Default.WithVolume(-2));
        ent.Comp.CurrentCookTimerTime = 10;
        Wzhzhzh(ent, args.Victim);
        UpdateUserInterfaceState(ent);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnSolutionChange(Entity<CookingDeviceComponent> ent, ref SolutionContainerChangedEvent args) => UpdateUserInterfaceState(ent);

    private void OnContentUpdate(EntityUid uid, CookingDeviceComponent component, ContainerModifiedMessage args)
    {
        if (component.Storage == args.Container)
            UpdateUserInterfaceState((uid, component));
    }

    private void OnInsertAttempt(Entity<CookingDeviceComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        if (ent.Comp.Broken)
        {
            args.Cancel();
            return;
        }

        if (TryComp<ItemComponent>(args.EntityUid, out var item))
        {
            if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
            {
                args.Cancel();
                return;
            }
        }
        else
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
            args.Cancel();
    }

    private void OnInteractUsing(Entity<CookingDeviceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!(TryComp<ApcPowerReceiverComponent>(ent, out var apc) && apc.Powered))
        {
            _popupSystem.PopupEntity(Loc.GetString("microwave-component-interact-using-no-power"), ent, args.User);
            return;
        }

        if (ent.Comp.Broken)
        {
            _popupSystem.PopupEntity(Loc.GetString("microwave-component-interact-using-broken"), ent, args.User);
            return;
        }

        if (TryComp<ItemComponent>(args.Used, out var item))
        {
            // check if size of an item you're trying to put in is too big
            if (_item.GetSizePrototype(item.Size) > _item.GetSizePrototype(ent.Comp.MaxItemSize))
            {
                _popupSystem.PopupEntity(Loc.GetString("microwave-component-interact-item-too-big", ("item", args.Used)), ent, args.User);
                return;
            }
        }
        else
        {
            // check if thing you're trying to put in isn't an item
            _popupSystem.PopupEntity(Loc.GetString("microwave-component-interact-using-transfer-fail"), ent, args.User);
            return;
        }

        if (ent.Comp.Storage.Count >= ent.Comp.Capacity)
        {
            _popupSystem.PopupEntity(Loc.GetString("microwave-component-interact-full"), ent, args.User);
            return;
        }

        args.Handled = true;
        _handsSystem.TryDropIntoContainer(args.User, args.Used, ent.Comp.Storage);
        UpdateUserInterfaceState(ent);
    }

    private void OnBreak(Entity<CookingDeviceComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.Broken = true;
        SetAppearance(ent, MicrowaveVisualState.Broken, ent.Comp);
        StopCooking(ent);
        _container.EmptyContainer(ent.Comp.Storage);
        UpdateUserInterfaceState(ent);
    }

    private void OnPowerChanged(Entity<CookingDeviceComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            SetAppearance(ent, MicrowaveVisualState.Idle, ent.Comp);
            StopCooking(ent);
        }
        UpdateUserInterfaceState(ent);
    }

    private void OnAnchorChanged(EntityUid uid, CookingDeviceComponent component, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            _container.EmptyContainer(component.Storage);
    }

    private void OnSignalReceived(Entity<CookingDeviceComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port != ent.Comp.OnPort)
            return;

        if (ent.Comp.Broken || !_power.IsPowered(ent))
            return;

        Wzhzhzh(ent, null);
    }

    public void UpdateUserInterfaceState(Entity<CookingDeviceComponent> ent, bool? IsBusy = null)
        => _userInterface.SetUiState(ent.Owner, MicrowaveUiKey.Key, new MicrowaveUpdateUserInterfaceState(
                GetNetEntityArray(ent.Comp.Storage.ContainedEntities.ToArray()),
                IsBusy ?? HasComp<ActiveCookingDeviceComponent>(ent.Owner),
                ent.Comp.Safe,
                ent.Comp.CurrentCookTimeButtonIndex,
                ent.Comp.CurrentCookTimerTime,
                ent.Comp.CurrentCookTimeEnd,
                ent.Comp.StartedCookTime
        ));

    public void SetAppearance(EntityUid uid, MicrowaveVisualState? state = null, CookingDeviceComponent? component = null, AppearanceComponent? appearanceComponent = null, bool? Opened = null)
    {
        if (!Resolve(uid, ref component, ref appearanceComponent, false))
            return;

        if (Opened != null)
        {
            var openedState = Opened.Value ? OpenableKitchenDevice.Opened : OpenableKitchenDevice.Closed;
            _appearance.SetData(uid, PowerDeviceVisuals.VisualState, openedState, appearanceComponent);
        }

        if (state == null)
            return;

        var display = component.Broken ? MicrowaveVisualState.Broken : state;
        _appearance.SetData(uid, PowerDeviceVisuals.VisualState, display, appearanceComponent);
    }

    public static bool HasContents(CookingDeviceComponent component) => component.Storage.ContainedEntities.Any();

    /// <summary>
    /// Explodes the microwave internally, turning it into a broken state, destroying its board, and spitting out its machine parts
    /// </summary>
    /// <param name="ent"></param>
    public void Explode(Entity<CookingDeviceComponent> ent)
    {
        ent.Comp.Broken = true; // Make broken so we stop processing stuff
        _explosion.TriggerExplosive(ent);
        if (TryComp<MachineComponent>(ent, out var machine))
        {
            _container.CleanContainer(machine.BoardContainer);
            _container.EmptyContainer(machine.PartContainer);
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent)} exploded from unsafe cooking!");
    }
    /// <summary>
    /// Handles the attempted cooking of unsafe objects
    /// </summary>
    /// <remarks>
    /// Returns false if the microwave didn't explode, true if it exploded.
    /// </remarks>
    private void RollMalfunction(Entity<ActiveCookingDeviceComponent, CookingDeviceComponent> ent)
    {
        if (ent.Comp1.MalfunctionTime == TimeSpan.Zero)
            return;

        if (ent.Comp1.MalfunctionTime > _gameTiming.CurTime)
            return;

        ent.Comp1.MalfunctionTime = _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp2.MalfunctionInterval);
        if (_random.Prob(ent.Comp2.ExplosionChance))
        {
            Explode((ent, ent.Comp2));
            return;  // microwave is fucked, stop the cooking.
        }

        if (_random.Prob(ent.Comp2.LightningChance))
            _lightning.ShootRandomLightnings(ent, 1.0f, 2, _malfunctionSpark, triggerLightningEvents: false);
    }

    /// <summary>
    /// Starts Cooking
    /// </summary>
    /// <remarks>
    /// It does not make a "wzhzhzh" sound, it makes a "mmmmmmmm" sound!
    /// -emo
    /// </remarks>
    public void Wzhzhzh(Entity<CookingDeviceComponent> ent, EntityUid? user)
    {
        if (!HasContents(ent.Comp) || HasComp<ActiveCookingDeviceComponent>(ent) || !(TryComp<ApcPowerReceiverComponent>(ent, out var apc) && apc.Powered))
            return;

        var solidsDict = new Dictionary<string, int>();
        var reagentDict = new Dictionary<string, FixedPoint2>();
        var malfunctioning = false;

        // TODO use lists of Reagent quantities instead of reagent prototype ids.
        foreach (var item in ent.Comp.Storage.ContainedEntities.ToArray())
        {
            // special behavior when being microwaved ;)
            var ev = new BeingMicrowavedEvent(ent, user);
            RaiseLocalEvent(item, ev);

            // TODO MICROWAVE SPARKS & EFFECTS
            // Various microwaveable entities should probably spawn a spark, play a sound, and generate a pop=up.
            // This should probably be handled by the microwave system, with fields in BeingMicrowavedEvent.

            if (ev.Handled)
            {
                UpdateUserInterfaceState(ent);
                return;
            }

            if (_tag.HasTag(item, _metalTag))
                malfunctioning = true;

            if (_tag.HasTag(item, _plasticTag))
            {
                var junk = Spawn(ent.Comp.BadRecipeEntityId, Transform(ent).Coordinates);
                _container.Insert(junk, ent.Comp.Storage);
                Del(item);
                continue;
            }

            var microwavedComp = AddComp<ActivelyCookedComponent>(item);
            microwavedComp.Microwave = ent;

            string? solidID = null;
            int amountToAdd = 1;

            // If a microwave recipe uses a stacked item, use the default stack prototype id instead of prototype id
            if (TryComp<StackComponent>(item, out var stackComp))
            {
                solidID = _prototype.Index<StackPrototype>(stackComp.StackTypeId).Spawn;
                amountToAdd = stackComp.Count;
            }
            else
            {
                var metaData = MetaData(item); //this simply begs for cooking refactor
                if (metaData.EntityPrototype is not null)
                    solidID = metaData.EntityPrototype.ID;
            }

            if (solidID is null)
                continue;

            if (!solidsDict.TryAdd(solidID, amountToAdd))
                solidsDict[solidID] += amountToAdd;

            // only use reagents we have access to
            // you have to break the eggs before we can use them!
            if (!_solutionContainer.TryGetDrainableSolution(item, out var _, out var solution))
                continue;

            foreach (var (reagent, quantity) in solution.Contents)
                if (!reagentDict.TryAdd(reagent.Prototype, quantity))
                    reagentDict[reagent.Prototype] += quantity;
        }

        // Check recipes
        var getRecipesEv = new GetSecretRecipesEvent();
        RaiseLocalEvent(ent, ref getRecipesEv);

        List<FoodRecipePrototype> recipes = getRecipesEv.Recipes;
        recipes.AddRange(_recipeManager.Recipes);
        var portionedRecipes = recipes.Select(r => CanSatisfyRecipe(ent.Comp, r, solidsDict, reagentDict)).Where(r => r.Item2 > 0).ToList();

        _audio.PlayPvs(ent.Comp.StartCookingSound, ent);

        ent.Comp.StartedCookTime = _gameTiming.CurTime;
        var activeComp = AddComp<ActiveCookingDeviceComponent>(ent); //microwave is now cooking

        activeComp.CookTimeRemaining = ent.Comp.CurrentCookTimerTime * ent.Comp.CookTimeMultiplier;
        activeComp.TotalTime = ent.Comp.CurrentCookTimerTime; //this doesn't scale so that we can have the "actual" time

        foreach (var recipe in portionedRecipes)
            if (!activeComp.PortionedRecipes.ContainsKey(recipe.Item1))
                activeComp.PortionedRecipes.Add(recipe.Item1, recipe.Item2);

        //Scale tiems with cook times
        ent.Comp.CurrentCookTimeEnd = _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp.CurrentCookTimerTime * ent.Comp.CookTimeMultiplier);
        if (malfunctioning)
            activeComp.MalfunctionTime = _gameTiming.CurTime + TimeSpan.FromSeconds(ent.Comp.MalfunctionInterval);
        UpdateUserInterfaceState(ent);
    }

    private void StopCooking(Entity<CookingDeviceComponent> ent)
    {
        RemCompDeferred<ActiveCookingDeviceComponent>(ent);
        foreach (var solid in ent.Comp.Storage.ContainedEntities)
            RemCompDeferred<ActivelyCookedComponent>(solid);
    }

    public static (FoodRecipePrototype, int) CanSatisfyRecipe(CookingDeviceComponent component, FoodRecipePrototype recipe, Dictionary<string, int> solids, Dictionary<string, FixedPoint2> reagents)
    {
        var portions = 0;

        if (component.Safe && component.CurrentCookTimerTime % recipe.CookTime != 0)
        {
            //can't be a multiple of this recipe
            return (recipe, 0);
        }

        if (recipe.DeviceType != component.DeviceType)
            return (recipe, 0);

        foreach (var solid in recipe.IngredientsSolids)
        {
            if (!solids.ContainsKey(solid.Key))
                return (recipe, 0);

            if (solids[solid.Key] < solid.Value)
                return (recipe, 0);

            portions = portions == 0
                ? solids[solid.Key] / solid.Value.Int()
                : Math.Min(portions, solids[solid.Key] / solid.Value.Int());
        }

        foreach (var reagent in recipe.IngredientsReagents)
        {
            // TODO Turn recipe.IngredientsReagents into a ReagentQuantity[]
            if (!reagents.ContainsKey(reagent.Key))
                return (recipe, 0);

            if (reagents[reagent.Key] < reagent.Value)
                return (recipe, 0);

            portions = portions == 0
                ? reagents[reagent.Key].Int() / reagent.Value.Int()
                : Math.Min(portions, reagents[reagent.Key].Int() / reagent.Value.Int());
        }

        //cook only as many of those portions as time allows
        return (recipe, component.Safe ? (int)Math.Min(portions, component.CurrentCookTimerTime / recipe.CookTime) : portions);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveCookingDeviceComponent, CookingDeviceComponent>();
        while (query.MoveNext(out var uid, out var active, out var cookingDevice))
        {

            active.CookTimeRemaining -= frameTime;

            RollMalfunction((uid, active, cookingDevice));

            //check if there's still cook time left
            int actualTime = (int)(_gameTiming.CurTime - cookingDevice.StartedCookTime).TotalSeconds;
            var coords = Transform(uid).Coordinates;
            if (active.CookTimeRemaining > 0 || (!cookingDevice.Safe && actualTime < 60))
            {
                AddTemperature(cookingDevice, frameTime);
                continue;
            }

            //this means the microwave has finished cooking.
            AddTemperature(cookingDevice, Math.Max(frameTime + active.CookTimeRemaining, 0)); //Though there's still a little bit more heat to pump out

            if (actualTime >= 60)
            {
                var containedItems = cookingDevice.Storage.ContainedEntities.ToList(); // error-proof copy
                foreach (var item in containedItems)
                {
                    string? itemID = null;

                    if (TryComp<StackComponent>(item, out var stackComp))
                        itemID = _prototype.Index<StackPrototype>(stackComp.StackTypeId).Spawn;
                    else
                    {
                        var metaData = MetaData(item);
                        if (metaData.EntityPrototype == null)
                            continue;
                        itemID = metaData.EntityPrototype.ID;
                    }

                    if (stackComp is not null)
                    {
                        if (stackComp.Count == 1)
                            _container.Remove(item, cookingDevice.Storage);
                        _stack.TryUse(item, 1);
                        Spawn(cookingDevice.SpoiledItemId, coords);
                        continue;
                    }
                    else
                    {
                        _container.Remove(item, cookingDevice.Storage);
                        Del(item);
                        Spawn(cookingDevice.SpoiledItemId, coords);
                        continue;
                    }
                }
            }

            foreach (var (recipe, availableAmount) in active.PortionedRecipes)
            {
                int targetTime = (int)recipe.CookTime;

                if (actualTime >= (targetTime - 1))
                {
                    for (var i = 0; i < availableAmount; i++)
                    {
                        if (SubtractContents(cookingDevice, recipe))
                            Spawn(recipe.Result, coords);
                        else
                            continue;
                    }
                }
            }

            _container.EmptyContainer(cookingDevice.Storage);
            cookingDevice.CurrentCookTimeEnd = TimeSpan.Zero;
            UpdateUserInterfaceState((uid, cookingDevice));
            _audio.PlayPvs(cookingDevice.FoodDoneSound, uid);
            StopCooking((uid, cookingDevice));
        }
    }

    /// <summary>
    /// This event tries to get secret recipes that the microwave might be capable of.
    /// Currently, we only check the microwave itself, but in the future, the user might be able to learn recipes.
    /// </summary>
    private void OnGetSecretRecipes(Entity<FoodRecipeProviderComponent> ent, ref GetSecretRecipesEvent args)
    {
        foreach (ProtoId<FoodRecipePrototype> recipeId in ent.Comp.ProvidedRecipes)
        {
            if (_prototype.Resolve(recipeId, out var recipeProto))
            {
                args.Recipes.Add(recipeProto);
            }
        }
    }

    #region ui

    private void OnStopMessage(Entity<CookingDeviceComponent> ent, ref MicrowaveStopCookMessage args)
    {
        var uid = ent.Owner;
        var cookingDevice = ent.Comp;

        if (!TryComp<ActiveCookingDeviceComponent>(ent.Owner, out var active))
            return;
        //this means the microwave has finished cooking.
        AddTemperature(cookingDevice, Math.Max((float)_gameTiming.CurTime.TotalSeconds + active.CookTimeRemaining, 0)); //Though there's still a little bit more heat to pump out
        int actualTime = (int)(_gameTiming.CurTime - cookingDevice.StartedCookTime).TotalSeconds;
        foreach (var (recipe, availableAmount) in active.PortionedRecipes)
        {
            int targetTime = (int)recipe.CookTime;
            var coords = Transform(uid).Coordinates;

            if (Math.Abs(targetTime - actualTime) <= 1)
            {
                for (var i = 0; i < availableAmount; i++)
                {
                    if (SubtractContents(cookingDevice, recipe))
                        Spawn(recipe.Result, coords);
                    else
                        continue;
                }
            }
        }

        _container.EmptyContainer(cookingDevice.Storage);
        cookingDevice.CurrentCookTimeEnd = TimeSpan.Zero;
        UpdateUserInterfaceState((uid, cookingDevice));
        _audio.PlayPvs(cookingDevice.FoodDoneSound, uid);
        StopCooking((uid, cookingDevice));
    }

    private void OnEjectMessage(Entity<CookingDeviceComponent> ent, ref MicrowaveEjectMessage args)
    {
        if (!HasContents(ent.Comp) || HasComp<ActiveCookingDeviceComponent>(ent))
            return;

        _container.EmptyContainer(ent.Comp.Storage);
        _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2));
        UpdateUserInterfaceState(ent);
    }

    private void OnEjectIndex(Entity<CookingDeviceComponent> ent, ref MicrowaveEjectSolidIndexedMessage args)
    {
        if (!HasContents(ent.Comp) || HasComp<ActiveCookingDeviceComponent>(ent))
            return;

        _container.Remove(GetEntity(args.EntityID), ent.Comp.Storage);
        UpdateUserInterfaceState(ent);
    }

    private void OnSelectTime(Entity<CookingDeviceComponent> ent, ref MicrowaveSelectCookTimeMessage args)
    {
        if (!HasContents(ent.Comp) || HasComp<ActiveCookingDeviceComponent>(ent) || !(TryComp<ApcPowerReceiverComponent>(ent, out var apc) && apc.Powered))
            return;

        // some validation to prevent trollage
        if (args.NewCookTime % 5 != 0 || args.NewCookTime > ent.Comp.MaxCookTime)
            return;

        ent.Comp.CurrentCookTimeButtonIndex = args.ButtonIndex;
        ent.Comp.CurrentCookTimerTime = args.NewCookTime;
        ent.Comp.CurrentCookTimeEnd = TimeSpan.Zero;
        _audio.PlayPvs(ent.Comp.ClickSound, ent, AudioParams.Default.WithVolume(-2));
        UpdateUserInterfaceState(ent);
    }
    #endregion
}
