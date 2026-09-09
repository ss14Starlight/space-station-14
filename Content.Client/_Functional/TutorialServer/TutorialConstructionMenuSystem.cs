using Content.Client.Construction;
using Content.Shared._Functional.TutorialServer;

namespace Content.Client._Functional.TutorialServer;

/// <summary>
/// Tells the server when the player opens the construction menu, so a curriculum can teach it.
/// </summary>
/// <remarks>
/// Hooks <see cref="ConstructionSystem.ToggleCraftingWindow"/> rather than the keybind itself, so
/// the report follows the menu however it was opened.
/// </remarks>
public sealed partial class TutorialConstructionMenuSystem : EntitySystem
{
    [Dependency] private ConstructionSystem _construction = default!;

    public override void Initialize()
    {
        base.Initialize();

        _construction.ToggleCraftingWindow += OnToggleCraftingWindow;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _construction.ToggleCraftingWindow -= OnToggleCraftingWindow;
    }

    /// <summary>
    /// Fires on the toggle, not on the open, so a player closing the menu also reports. Harmless:
    /// the beat only asks that they found it once, and they cannot close what they never opened.
    /// </summary>
    private void OnToggleCraftingWindow(object? sender, EventArgs args)
    {
        RaiseNetworkEvent(new TutorialConstructionMenuOpenedEvent());
    }
}
