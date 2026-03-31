using System.Linq;
using Content.Shared._Moffstation.Clothing.ModularHud.Components;
using Content.Shared._Moffstation.Extensions;
using Content.Shared.Access.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Contraband;
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

/// This system implements the behavior of <see cref="ModularHudComponent"/>s. It hands three basic things:
/// <list type="bullet">
/// <item>Visuals</item>
/// <item>Basic interactions</item>
/// <item>HUD effect relaying</item>
/// </list>
///
/// <b>Visuals</b>
/// More or less just <see cref="SyncVisuals"/>, sets <see cref="AppearanceComponent"/>'s data to colors determined by
/// <see cref="ModularHudModuleComponent.Visuals"/> for the client visualizer system to handle.
///
/// <b>Basic Interactions</b>
/// Insertion / extraction of modules, examine implementations, verbs, the usual component stuff.
///
/// <b>HUD Effect Relaying</b>
/// This is the real power of modular HUDs. HUD effects on preexisting entities are implemented by raising events on the
/// wearer of the HUD, and then a system will relay those events to worn entities, allowing the HUD entity to handle the
/// event. This system does basically the same thing, except instead of the clothing with <see cref="ModularHudComponent"/>
/// handling the events directly, it again relays these HUD effect events to <see cref="ModularHudComponent.ModuleContainer"/>'s
/// contents. The modules have the same components as the preexisting HUD entities, thus handling the events to achieve
/// the HUD effects.
public abstract partial class SharedModularHudSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly BlurryVisionSystem _blurryVision = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    /// The lists contained here specify the layers and (by implicit ordering) precedence for each visual category. For
    /// example, the highest priority lens color will apply to `Lens`, then the next highest will apply to
    /// `LensAccentMajor`, and so on.
    /// Presently, this only really matters for lens colors, but it simplifies the logic to treat all the categories the
    /// same.
    private static readonly Dictionary<ModularHudVisuals, List<ModularHudVisualKeys>> VisualsLayersToKeys = new()
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

        SubscribeLocalEvent<ModularHudComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ModularHudComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<ModularHudComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ModularHudComponent, GotUnequippedEvent>(OnGotUneqipped);
        SubscribeLocalEvent<ModularHudComponent, EntInsertedIntoContainerMessage>(OnContainerModifiedMessage);
        SubscribeLocalEvent<ModularHudComponent, EntRemovedFromContainerMessage>(OnContainerModifiedMessage);
        SubscribeLocalEvent<ModularHudComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ModularHudComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ModularHudComponent, HudModulesRemovalDoAfterEvent>(OnHudModulesRemovalDoAfter);
        SubscribeLocalEvent<ModularHudComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);

        // Relays for module events.
        SubscribeRelaysForEffectEvents<GetContrabandDetailsEvent>();
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

            var modules = GetModules(entity);
            foreach (var module in predicate is { } p ? modules.Where(p) : modules)
            {
                RaiseLocalEvent(module, ref args);
            }
        }
    );

    /// Yields all the modules contained in the given HUD.
    public IEnumerable<Entity<ModularHudModuleComponent>> GetModules(Entity<ModularHudComponent> entity)
    {
        foreach (var moduleEnt in entity.Comp.ModuleContainer?.ContainedEntities ?? [])
        {
            if (TryComp<ModularHudModuleComponent>(moduleEnt, out var moduleComp))
                yield return (moduleEnt, moduleComp);
        }
    }

    private void OnStartup(Entity<ModularHudComponent> entity, ref ComponentStartup args)
    {
        entity.Comp.ModuleContainer = _container.EnsureContainer<Container>(entity, entity.Comp.ModuleContainerId);
        RefreshEffectsForModules(GetModules(entity));
        SyncVisuals(entity);
    }

    private void OnComponentRemove(Entity<ModularHudComponent> entity, ref ComponentRemove args)
    {
        RefreshEffectsForModules(GetModules(entity));
    }

    private void OnInteractUsing(Entity<ModularHudComponent> entity, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Module insertion
        if (TryComp<ModularHudModuleComponent>(args.Used, out var moduleComp))
        {
            if (entity.Comp.ModuleContainer is null)
            {
                this.AssertOrLogError("Container should be initialized");
                return;
            }

            if (entity.Comp.NumContainedModules >= entity.Comp.MaximumContainedModules)
            {
                _popup.PopupPredictedCursor(
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
                _popup.PopupPredictedCursor(
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
        if (GetModules(entity).Any())
        {
            var usedHasQuality = _tool.HasQuality(args.Used, entity.Comp.ModuleExtractionToolQuality);
            _proto.Resolve(entity.Comp.ModuleExtractionToolQuality, out var toolQuality);

            if (!usedHasQuality)
            {
                _popup.PopupPredictedCursor(
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

    private void OnGetInteractionVerbs(Entity<ModularHudComponent> entity, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanComplexInteract)
            return;

        // Module insertion
        if (args.Using is { } used && TryComp<ModularHudModuleComponent>(args.Using, out var moduleComp))
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
        if (GetModules(entity).Any())
        {
            var tool = args.Using;
            var user = args.User;
            var usedHasQuality = args.Using is { } u && _tool.HasQuality(u, entity.Comp.ModuleExtractionToolQuality);
            _proto.Resolve(entity.Comp.ModuleExtractionToolQuality, out var toolQuality);
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(entity.Comp.RemoveModulesVerbText),
                Act = () =>
                {
                    _tool.UseTool(
                        tool!.Value, // Can only invoke the verb if `tool` is not null
                        user,
                        entity.Owner,
                        entity.Comp.ModuleRemovalDelay,
                        [entity.Comp.ModuleExtractionToolQuality],
                        new HudModulesRemovalDoAfterEvent(),
                        out _
                    );
                },
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
    private void OnExamined(Entity<ModularHudComponent> entity, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(ModularHudComponent)))
        {
            using var modules = GetModules(entity).GetEnumerator();
            if (!modules.MoveNext())
            {
                args.PushMarkup(Loc.GetString(entity.Comp.NoModulesExamineText));
                return;
            }

            args.PushMarkup(Loc.GetString(entity.Comp.HeaderExamineText));
            do
            {
                args.PushMarkup(Loc.GetString(entity.Comp.ModuleItemExamineText, ("module", modules.Current)));
            } while (modules.MoveNext());
        }
    }

    /// Removes all modules from this HUD when the doafter is completed.
    private void OnHudModulesRemovalDoAfter(Entity<ModularHudComponent> entity, ref HudModulesRemovalDoAfterEvent args)
    {
        if (args.Cancelled || entity.Comp.ModuleContainer is null)
            return;

        foreach (var module in GetModules(entity).ToList())
        {
            _container.Remove(module.Owner, entity.Comp.ModuleContainer);
            _hands.TryPickup(args.User, module);
        }
    }

    /// Refresh the effects provided by the module added/removed.
    private void OnContainerModifiedMessage<TArgs>(
        Entity<ModularHudComponent> entity,
        ref TArgs args
    ) where TArgs : ContainerModifiedMessage
    {
        if (args.Container.ID != entity.Comp.ModuleContainerId ||
            !TryComp<ModularHudModuleComponent>(args.Entity, out var moduleComp))
            return;

        RefreshEffectsForModules([(args.Entity, moduleComp)]);
        SyncVisuals(entity);
    }

    private void OnGotEquipped(Entity<ModularHudComponent> entity, ref GotEquippedEvent args)
    {
        RefreshEffectsForWearerForContainedModules(entity, args.Equipee);
    }

    private void OnGotUneqipped(Entity<ModularHudComponent> entity, ref GotUnequippedEvent args)
    {
        RefreshEffectsForWearerForContainedModules(entity, args.Equipee);
    }

    /// This function contains a functional grab-bag of whatever function calls / event raisings need to happen to cause
    /// the disparate HUD effects to be updated when the modular HUD is un/equipped.
    private void RefreshEffectsForWearerForContainedModules(Entity<ModularHudComponent> entity, EntityUid equippee)
    {
        _blurryVision.UpdateBlurMagnitude(equippee);
        var flashEv = new FlashImmunityChangedEvent(_flash.IsFlashImmune(equippee));
        RaiseLocalEvent(equippee, ref flashEv);

        RefreshEffectsForModules(GetModules(entity));
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
    )
    {
        return module.Comp.Requirements.Where(it => !_whitelist.IsWhitelistPassOrNull(it.Whitelist, entity) ||
                                                    _whitelist.IsWhitelistPass(it.Blacklist, entity))
            .Select(it => Loc.GetString(it.FailureMessage, ("hud", entity), ("module", module)));
    }

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
        foreach (var (layer, color) in GetModules(entity)
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
            var prioritizedColorsAndKeys = prioritizedColors.Zip(VisualsLayersToKeys[visualsLayer]).ToList();

            // If there are no colors from modules for this layer, use the default color from the HUD.
            if (prioritizedColorsAndKeys.Count == 0)
            {
                _appearance.SetData(
                    entity,
                    VisualsLayersToKeys[visualsLayer].First(),
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

    /// This doafter event is raised when the doafter to remove the HUD's modules is complete.
    [Serializable, NetSerializable]
    private sealed partial class HudModulesRemovalDoAfterEvent : SimpleDoAfterEvent;
}
