using Content.Shared.Actions;
using Content.Shared.Ninja.Components;
using Content.Shared._Starlight.GeneralItemCreator.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.GeneralItemCreator.Components;

// Just a copy of the ninja's.

/// <summary>
/// Uses battery charge to spawn an item and place it in the user's hands.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGeneralItemCreatorSystem))]
public sealed partial class GeneralItemCreatorComponent : ItemCreatorComponent;

/// <summary>
/// Action event to use an <see cref="ItemCreator"/>.
/// </summary>
public sealed partial class GeneralCreateItemEvent : InstantActionEvent;
