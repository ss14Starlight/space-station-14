using System.Linq;
using Content.Shared._Moffstation.Clothing.ModularHud.Components;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Access.Systems;
using Content.Shared.Chemistry;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using static Content.Shared._Moffstation.Clothing.ModularHud.Components.ModularHudVisualKeys;

namespace Content.Shared._Moffstation.Clothing.ModularHud.Systems;

/// This system implements the behavior of <see cref="ModularHudComponent"/>s. It handles three basic things:
/// <list type="bullet">
/// <item>Visuals</item>
/// <item>Basic interactions</item>
/// <item>HUD effect relaying</item>
/// </list>
///
/// <b>Visuals</b>
/// More or less just <see cref="SyncVisuals"/>, sets <see cref="AppearanceComponent"/>'s data to colors determined by
/// <see cref="ModularHudModuleComponent.Visuals"/> for the client visualizer system to handle.
/// <br/>
/// <b>Basic Interactions</b>
/// Insertion / extraction of modules, examine implementations, verbs, the usual component stuff.
/// <br/>
/// <b>HUD Effect Relaying</b>
/// This is the real power of modular HUDs. HUD effects on preexisting entities are implemented by raising events on the
/// wearer of the HUD, and then a system will relay those events to worn entities, allowing the HUD entity to handle the
/// event. This system does basically the same thing, except instead of the clothing with <see cref="ModularHudComponent"/>
/// handling the events directly, it again relays these HUD effect events to <see cref="ModularHudComponent.ModuleContainer"/>'s
/// contents. The modules have the same components as the preexisting HUD entities, thus handling the events to achieve
/// the HUD effects.
public abstract partial class SharedModularHudSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private BlurryVisionSystem _blurryVision = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedFlashSystem _flash = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [Dependency] private EntityQuery<ModularHudModuleComponent> _modularHudModuleQuery;

    /// The lists contained here specify the layers and (by implicit ordering) precedence for each visual category. For
    /// example, the highest priority lens color will apply to `Lens`, then the next highest will apply to
    /// `LensAccentMajor`, and so on.
    /// Presently, this only really matters for lens colors, but it simplifies the logic to treat all the categories the
    /// same.
    private static readonly Dictionary<ModularHudVisuals, List<ModularHudVisualKeys>> _visualsLayersToKeys = new()
    {
        // ReSharper disable once UseCollectionExpression // Whatever the underlying thing that enables collection expressions for statics is is not whitelisted in Robust, so fuck me, I guess.
        [ModularHudVisuals.Lens] = new List<ModularHudVisualKeys> { Lens, LensAccentMajor, LensAccentMinor },
        // ReSharper disable once UseCollectionExpression
        [ModularHudVisuals.Accent] = new List<ModularHudVisualKeys> { Accent },
        // ReSharper disable once UseCollectionExpression
        [ModularHudVisuals.Specular] = new List<ModularHudVisualKeys> { Specular },
    };

    public override void Initialize()
    {
        base.Initialize();

        // Relays for module events.
        SubscribeRelaysForEffectEvents<SolutionScanEvent>();
        SubscribeRelaysForEffectEvents<ShowAccessReaderSettingsEvent>();
        SubscribeRelaysForEffectEvents<GetEyeProtectionEvent>();
        SubscribeRelaysForEffectEvents<SeeIdentityAttemptEvent>();
        SubscribeRelaysForEffectEvents<FlashAttemptEvent>();
        SubscribeRelaysForEffectEvents<GetBlurEvent>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowJobIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowHealthBarsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowHealthIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowHungerIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowThirstIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowMindShieldIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowSyndicateIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ShowCriminalRecordIconsComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<BlackAndWhiteOverlayComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<NoirOverlayComponent>>();
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<BlueNoirOverlayComponent>>(); // Starlight
        SubscribeRelaysForEffectEvents<RefreshEquipmentHudEvent<ThermalSightComponent>>(); // Starlight
    }

    /// Adds subscriptions which relay `TArgs` to all contained modules.
    /// <param name="requiresActiveSlots">Events will only be relayed while in <see cref="ModularHudComponent.ActiveSlots"/></param>
    /// <param name="predicate">Events are relayed only to modules which return true when passed to this function. If null, all modules are relayed to.</param>
    protected void SubscribeRelaysForEffectEvents<TArgs>(
        bool requiresActiveSlots = true,
        Func<Entity<ModularHudModuleComponent>, bool>? predicate = null
    ) where TArgs : notnull => Subs.SubscribeWithRelay(
        delegate(Entity<ModularHudComponent> entity, ref TArgs args)
        {
            // Only relay if we're in the slots which this HUD is active in.
            if (requiresActiveSlots && !_inventory.InSlotWithAnyFlags(entity.Owner, entity.Comp.ActiveSlots))
                return;

            var modules = GetModules(entity.AsNullable()); // Starlight
            foreach (var module in predicate is { } p ? modules.Where(p) : modules)
            {
                RaiseLocalEvent(module, ref args);
            }
        }
    );

    /// Yields all the modules contained in the given HUD.
    public IEnumerable<Entity<ModularHudModuleComponent>> GetModules(Entity<ModularHudComponent?> entity) // Starlight
    {
        if (!Resolve(entity, ref entity.Comp)) // Starlight
            return []; // Starlight

        return GetContainedModules(entity.Comp); // Starlight
    }

    // Starlight start
    private IEnumerable<Entity<ModularHudModuleComponent>> GetContainedModules(ModularHudComponent component)
    {
        foreach (var moduleEnt in component.ModuleContainer?.ContainedEntities ?? []) // Starlight end
        {
            if (_modularHudModuleQuery.TryComp(moduleEnt, out var moduleComp))
                yield return (moduleEnt, moduleComp);
        }
    }

    [SubscribeLocalEvent]
    private void OnStartup(Entity<ModularHudComponent> entity, ref ComponentStartup args)
    {
        entity.Comp.ModuleContainer = _container.EnsureContainer<Container>(entity, entity.Comp.ModuleContainerId);
        RefreshEffectsForModules(GetModules(entity.AsNullable())); // Starlight
        SyncVisuals(entity);
    }

    [SubscribeLocalEvent]
    private void OnComponentRemove(Entity<ModularHudComponent> entity, ref ComponentRemove args) => RefreshEffectsForModules(GetContainedModules(entity.Comp)); // Starlight

    [SubscribeLocalEvent]
    private void OnInteractUsing(Entity<ModularHudComponent> entity, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Module insertion
        if (_modularHudModuleQuery.TryComp(args.Used, out var moduleComp))
        {
            if (entity.Comp.ModuleContainer is null)
            {
                this.AssertOrLogError("Container should be initialized");
                return;
            }

            if (entity.Comp.NumContainedModules >= entity.Comp.MaximumContainedModules)
            {
                _popup.PopupCursor(
                    Loc.GetString(
                        entity.Comp.ModuleSlotsFullErrorText,
                        ("hud", Name(entity))
                    ),
                    args.User
                );
                return;
            }

            var moduleFailureReqs = GetRequirementFailures(entity, (args.Used, moduleComp)).ToList();
            if (moduleFailureReqs.Count != 0)
            {
                _popup.PopupCursor(
                    string.Join(", ", moduleFailureReqs),
                    args.User
                );
                return;
            }

            if (!_container.Insert(args.Used, entity.Comp.ModuleContainer))
            {
                // This should always succeed, so error out if it doesn't.
                this.AssertOrLogError($"Failed to insert {ToPrettyString(args.Used)} into {ToPrettyString(entity)}");
            }

            args.Handled = true;
            return;
        }

        // Module removal
        if (GetModules(entity.AsNullable()).Any()) // Starlight
        {
            var usedHasQuality = _tool.HasQuality(args.Used, entity.Comp.ModuleExtractionToolQuality);
            _proto.Resolve(entity.Comp.ModuleExtractionToolQuality, out var toolQuality);

            if (!usedHasQuality)
            {
                _popup.PopupCursor(
                    Loc.GetString(
                        entity.Comp.MissingToolQualityErrorText,
                        ("quality", Loc.GetString(toolQuality?.Name ?? "Unknown")),
                        ("hud", Name(entity))
                    ),
                    args.User
                );
                return;
            }

            _tool.UseTool(
                args.Used,
                args.User,
                entity.Owner,
                entity.Comp.ModuleRemovalDelay,
                [entity.Comp.ModuleExtractionToolQuality],
                new HudModulesRemovalDoAfterEvent(),
                out _
            );
            args.Handled = true;
            return;
        }
    }

    [SubscribeLocalEvent]
    private void OnGetInteractionVerbs(Entity<ModularHudComponent> entity, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanComplexInteract)
            return;

        // Module insertion
        if (args.Using is { } used && _modularHudModuleQuery.TryComp(args.Using, out var moduleComp))
        {
            if (entity.Comp.ModuleContainer is null)
            {
                this.AssertOrLogError("Container should be initialized");
                return;
            }

            LocId? disabledReason = null;
            if (entity.Comp.NumContainedModules >= entity.Comp.MaximumContainedModules)
            {
                // Slots full
                disabledReason = entity.Comp.ModuleSlotsFullErrorText;
            }
            else
            {
                // Fails requirements
                var moduleFailureReqs = GetRequirementFailures(entity, (used, moduleComp)).ToList();
                if (moduleFailureReqs.Count != 0)
                    disabledReason = string.Join("; ", moduleFailureReqs);
            }

            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(entity.Comp.InsertModuleVerbText),
                Act = () =>
                {
                    if (disabledReason != null)
                        return;

                    if (!_container.Insert(used, entity.Comp.ModuleContainer))
                    {
                        // This should always succeed, so error out if it doesn't.
                        this.AssertOrLogError($"Failed to insert {ToPrettyString(used)} into {ToPrettyString(entity)}");
                    }
                },
                IconEntity = GetNetEntity(used),
                Message = Loc.GetString(
                    disabledReason ?? entity.Comp.InsertModuleVerbMessage,
                    ("module", Name(used)),
                    ("hud", Name(entity))
                ),
                Disabled = disabledReason != null,
            });
        }

        // Module removal
        if (GetModules(entity.AsNullable()).Any()) // Starlight
        {
            var tool = args.Using;
            var user = args.User;
            var usedHasQuality = args.Using is { } u && _tool.HasQuality(u, entity.Comp.ModuleExtractionToolQuality);
            _proto.Resolve(entity.Comp.ModuleExtractionToolQuality, out var toolQuality);
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(entity.Comp.RemoveModulesVerbText),
                Act = () => _tool.UseTool(
                        tool!.Value, // Can only invoke the verb if `tool` is not null
                        user,
                        entity.Owner,
                        entity.Comp.ModuleRemovalDelay,
                        [entity.Comp.ModuleExtractionToolQuality],
                        new HudModulesRemovalDoAfterEvent(),
                        out _
                    ),
                Message = Loc.GetString(
                    usedHasQuality
                        ? entity.Comp.RemoveModulesVerbMessage
                        : entity.Comp.MissingToolQualityErrorText,
                    ("quality", Loc.GetString(toolQuality?.Name ?? "Unknown")),
                    ("hud", Name(entity))
                ),
                Disabled = !usedHasQuality,
                Icon = toolQuality?.Icon,
            });
        }
    }

    /// Describes what, if anything, is in this HUD.
    [SubscribeLocalEvent]
    private void OnExamined(Entity<ModularHudComponent> entity, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(ModularHudComponent)))
        {
            using var modules = GetModules(entity.AsNullable()).GetEnumerator(); // Starlight
            if (!modules.MoveNext())
            {
                args.PushMarkup(Loc.GetString(entity.Comp.NoModulesExamineText));
            }
            else
            {
                args.PushMarkup(Loc.GetString(entity.Comp.HeaderExamineText));
                do
                {
                    args.PushMarkup(Loc.GetString(entity.Comp.ModuleItemExamineText, ("module", modules.Current)));
                } while (modules.MoveNext());
            }

            args.PushMarkup(Loc.GetString(entity.Comp.CapacityExamineText,
                ("capacity", entity.Comp.MaximumContainedModules)));
        }
    }

    /// Removes all modules from this HUD when the doafter is completed.
    [SubscribeLocalEvent]
    private void OnHudModulesRemovalDoAfter(Entity<ModularHudComponent> entity, ref HudModulesRemovalDoAfterEvent args)
    {
        if (args.Cancelled || entity.Comp.ModuleContainer is null)
            return;

        foreach (var module in GetModules(entity.AsNullable()).ToList()) // Starlight
        {
            _container.Remove(module.Owner, entity.Comp.ModuleContainer);
            _hands.TryPickup(args.User, module);
        }
    }

    [SubscribeLocalEvent]
    private void OnEntInsertedIntoContainerMessage(
        Entity<ModularHudComponent> entity,
        ref EntInsertedIntoContainerMessage args
    )
    {
        if (args.Container.ID != entity.Comp.ModuleContainerId ||
            !_modularHudModuleQuery.HasComp(args.Entity))
            return;

        RefreshVisualsAndEffects(entity, equippee: null);
    }

    [SubscribeLocalEvent]
    private void OnEntRemovedFromContainerMessage(
        Entity<ModularHudComponent> entity,
        ref EntRemovedFromContainerMessage args
    )
    {
        if (args.Container.ID != entity.Comp.ModuleContainerId ||
            !_modularHudModuleQuery.TryComp(args.Entity, out var comp))
            return;

        RefreshVisualsAndEffects(entity, equippee: null, [(args.Entity, comp)]);
    }

    [SubscribeLocalEvent]
    private void OnGotEquipped(Entity<ModularHudComponent> entity, ref GotEquippedEvent args) => RefreshVisualsAndEffects(entity, args.EquipTarget); // Starlight

    [SubscribeLocalEvent]
    private void OnGotUneqipped(Entity<ModularHudComponent> entity, ref GotUnequippedEvent args) => RefreshVisualsAndEffects(entity, args.EquipTarget); // Starlight

    /// This function contains a functional grab-bag of whatever function calls / event raisings need to happen to cause
    /// the disparate HUD effects to be updated when the modular HUD is un/equipped.
    /// If <paramref name="equippee"/> is passed in as null, will use <see cref="GetWearer"/> to try to find an equippee.
    /// <paramref name="extraModules"/> is provided to allow for removed modules to have their effects updated.
    private void RefreshVisualsAndEffects(
        Entity<ModularHudComponent> entity,
        EntityUid? equippee,
        IEnumerable<Entity<ModularHudModuleComponent>>? extraModules = null
    )
    {
        if ((equippee ?? GetWearer(entity)) is { } e)
        {
            _blurryVision.UpdateBlurMagnitude(e);
            var flashEv = new FlashImmunityChangedEvent(_flash.IsFlashImmune(e));
            RaiseLocalEvent(e, ref flashEv);
        }

        RefreshEffectsForModules(GetModules(entity.AsNullable()).Concat(extraModules ?? [])); // Starlight
        SyncVisuals(entity);
    }

    /// Raises <see cref="EquipmentHudNeedsRefreshEvent"/> on all modules in this HUD.
    private void RefreshEffectsForModules(IEnumerable<Entity<ModularHudModuleComponent>> modules)
    {
        var ev = new EquipmentHudNeedsRefreshEvent();
        foreach (var module in modules)
        {
            RaiseLocalEvent(module, ref ev);
        }
    }

    private IEnumerable<string> GetRequirementFailures(
        Entity<ModularHudComponent> entity,
        Entity<ModularHudModuleComponent> module
    ) => module.Comp.Requirements.Where(it => !_whitelist.IsWhitelistPassOrNull(it.Whitelist, entity) || _whitelist.IsWhitelistPass(it.Blacklist, entity))
            .Select(it => Loc.GetString(it.FailureMessage, ("hud", entity), ("module", module)));

    /// Sets <see cref="AppearanceComponent"/> data for the given HUD entity, as appropriate. This causes the client to
    /// update the visuals for the HUD entity.
    private void SyncVisuals(Entity<ModularHudComponent, ModularHudVisualsComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp2))
            return;

        // Assemble the visuals data from the contained modules and defaults.
        var visuals = new Dictionary<ModularHudVisuals, PriorityQueue<ModularHudModuleComponent.ModuleColor>>()
        {
            // Lens gets three colors because there're three lens layers.
            [ModularHudVisuals.Lens] = new(3),
            [ModularHudVisuals.Accent] = new(1),
            [ModularHudVisuals.Specular] = new(1),
        };
        foreach (var (layer, color) in GetModules(entity.AsNullable()) // Starlight
                    .SelectMany(module => module.Comp.Visuals)
                    .Concat(entity.Comp2.InnateVisuals))
        {
            visuals[layer].Add(color);
        }

        var appearance = CompOrNull<AppearanceComponent>(entity);

        // Clear all of the visuals data.
        foreach (var key in Enum.GetValues<ModularHudVisualKeys>())
        {
            _appearance.SetData(entity, key, ModularHudVisualData.Invisible, appearance);
        }

        foreach (var (visualsLayer, prioritizedColors) in visuals.AsEnumerable())
        {
            var prioritizedColorsAndKeys = prioritizedColors.Zip(_visualsLayersToKeys[visualsLayer]).ToList();

            // If there are no colors from modules for this layer, use the default color from the HUD.
            if (prioritizedColorsAndKeys.Count == 0)
            {
                _appearance.SetData(
                    entity,
                    _visualsLayersToKeys[visualsLayer].First(),
                    new ModularHudVisualData(entity.Comp2.DefaultVisuals[visualsLayer]),
                    appearance
                );
                continue;
            }

            // If the highest priority color prevents other colors from applying, take only that color.
            var prioritizedColorsAndKeysWithPrevention = prioritizedColorsAndKeys.First().First.PreventsOtherColors
                ? prioritizedColorsAndKeys.Take(1)
                : prioritizedColorsAndKeys;

            foreach (var (mColor, key) in prioritizedColorsAndKeysWithPrevention)
            {
                _appearance.SetData(entity, key, new ModularHudVisualData(mColor.Color), appearance);
            }
        }

        if (entity.Comp2.FrameIsDynamic)
        {
            // If the frame is dynamic just tell the visuals system to render it.
            _appearance.SetData(entity, Frame, new ModularHudVisualData(Color.White), appearance);
        }
        else
        {
            // Otherwise, remove the appearance data so that the visuals system doesn't try to do anything with it.
            _appearance.RemoveData(entity, Frame, appearance);
        }
    }

    /// Gets the entity wearing <paramref name="hud"/> if it's equipped in <see cref="ModularHudComponent.ActiveSlots"/>.
    /// Returns null if the given HUD is not being worn or is worn in an inactive slot (eg. pocket).
    private EntityUid? GetWearer(Entity<ModularHudComponent> hud)
    {
        var parentUid = Transform(hud).ParentUid;
        return HasComp<InventoryComponent>(parentUid) &&
            _inventory.InSlotWithAnyFlags(hud.Owner, hud.Comp.ActiveSlots)
            ? parentUid
            : null;
    }

    /// This doafter event is raised when the doafter to remove the HUD's modules is complete.
    [Serializable, NetSerializable]
    private sealed partial class HudModulesRemovalDoAfterEvent : SimpleDoAfterEvent;
}
