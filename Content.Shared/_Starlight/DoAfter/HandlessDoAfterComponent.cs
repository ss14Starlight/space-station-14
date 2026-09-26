using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.DoAfter;

/// <summary>
/// Allows an entity with no hands to perform a do-after that set <see cref="Content.Shared.DoAfter.DoAfterArgs.NeedHand"/>.
/// Intended for cyborgs, whose hands only exist while a module is selected.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HandlessDoAfterComponent : Component;
