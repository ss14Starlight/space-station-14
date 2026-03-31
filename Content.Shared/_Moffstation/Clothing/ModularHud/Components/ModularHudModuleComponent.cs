using Content.Shared._Moffstation.Clothing.ModularHud.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Moffstation.Clothing.ModularHud.Components;

/// This component marks an entity as a modular HUD module. It's basically a marker component, but it does also have
/// visual details. In order for a module's effects to work, they need special relaying in <see cref="SharedModularHudSystem"/>.
[RegisterComponent, NetworkedComponent, Access(typeof(SharedModularHudSystem))]
public sealed partial class ModularHudModuleComponent : Component
{
    /// The visuals this module confers to its contained HUD when inserted.
    [DataField]
    public Dictionary<ModularHudVisuals, ModuleColor> Visuals = new();

    [DataField]
    public List<Requirement> Requirements = [];

    /// The requirements for inserting a module into a HUD.
    /// <param name="FailureMessage">What to show the user as an explanation for why the module doesn't fit</param>
    /// <param name="Whitelist">If not null, only HUDs which pass this whitelist may have this module inserted. Does nothing if null.</param>
    /// <param name="Blacklist">If not null, HUDs which pass this blacklist may not have this module inserted. Does nothing if null.</param>
    [DataRecord, Serializable, NetSerializable]
    public readonly partial record struct Requirement(
        LocId FailureMessage,
        EntityWhitelist? Whitelist = null,
        EntityWhitelist? Blacklist = null
    );

    /// A color in a <see cref="ModularHudModuleComponent"/>'s visuals.
    /// <param name="Color">The actual color</param>
    /// <param name="Priority">The color's priority. This allows for multiple modules to specify colors for the same layer, though only the highest priority color will be shown.</param>
    /// <param name="PreventsOtherColors">If true, and is highest priority, prevents other modules' colors from affecting this layer.</param>
    [DataRecord, Serializable, NetSerializable]
    public readonly partial record struct ModuleColor(Color Color, int Priority = 0, bool PreventsOtherColors = false)
        : IComparable<ModuleColor>
    {
        /// Implementation of <see cref="IComparable"/>, just defers to priorities. This is needed to use a priority queue
        /// in <see cref="SharedModularHudSystem.SyncVisuals"/>.
        public int CompareTo(ModuleColor other)
        {
            return Priority.CompareTo(other.Priority);
        }

        /// <summary>The actual color</summary>
        public Color Color { get; init; } = Color;
    }
}
