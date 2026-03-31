using Content.Shared._Moffstation.Clothing.ModularHud.Systems;
using Content.Shared.Inventory;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Moffstation.Clothing.ModularHud.Components;

/// This component marks the given entity as a Modular HUD. This means it can have entities with
/// <see cref="ModularHudModuleComponent"/>s inserted, and when worn in <see cref="ActiveSlots"/>, conveys the effects
/// of those modules to the wearer.
/// <seealso cref="SharedModularHudSystem"/>
/// <seealso cref="ModularHudModuleComponent"/>
/// <seealso cref="ModularHudVisualsComponent"/>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedModularHudSystem))]
public sealed partial class ModularHudComponent : Component
{
    /// While worn in these slots, the HUD conveys its effects to the wearer.
    [DataField]
    public SlotFlags ActiveSlots = SlotFlags.WITHOUT_POCKET;

    /// The ID of <see cref="ModuleContainer"/>.
    [DataField(required: true)]
    public string ModuleContainerId = default!;

    /// The container which contains the entities with <see cref="ModularHudModuleComponent"/> which this HUD contains.
    [ViewVariables]
    public Container? ModuleContainer;

    /// The number of modules in <see cref="ModuleContainer"/>.
    [ViewVariables]
    public int NumContainedModules => ModuleContainer?.Count ?? 0;

    /// The maximum number of modules this HUD can contain.
    [DataField(required: true)]
    public int MaximumContainedModules;

    /// And entity with <see cref="ToolComponent"/> and whose <see cref="ToolComponent.Qualities"/> includes this can
    /// be used to extract the modules from this HUD.
    [DataField(required: true)]
    public ProtoId<ToolQualityPrototype> ModuleExtractionToolQuality;

    /// How long it takes to extract the modules from this HUD.
    [DataField(required: true)]
    public TimeSpan ModuleRemovalDelay;

    // Localization strings and icons for verbs, examination, etc.
    [DataField] public LocId InsertModuleVerbText = "modularhud-verb-insert-module";
    [DataField] public LocId InsertModuleVerbMessage = "modularhud-verb-insert-module-message";

    [DataField]
    public LocId ModuleFailsRequirementsErrorText = "modularhud-verb-insert-module-error-fails-requirements";

    [DataField] public LocId ModuleSlotsFullErrorText = "modularhud-verb-insert-module-error-slots-full";

    [DataField] public LocId RemoveModulesVerbText = "modularhud-verb-remove-modules";
    [DataField] public LocId RemoveModulesVerbMessage = "modularhud-verb-remove-modules-message";
    [DataField] public LocId MissingToolQualityErrorText = "modularhud-verb-remove-modules-error-missing-tool-quality";
    [DataField] public LocId NoModulesToRemovePopupText = "modularhud-verb-remove-modules-error-no-modules-to-remove";

    [DataField] public LocId NoModulesExamineText = "modularhud-examine-no-modules";
    [DataField] public LocId HeaderExamineText = "modularhud-examine-modules-header";
    [DataField] public LocId ModuleItemExamineText = "modularhud-examine-module-item";

    [DataField] public SpriteSpecifier RemoveModuleIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png"));
}
