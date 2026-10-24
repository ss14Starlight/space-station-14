using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.IdentityManagement.Components;

/// <summary>
/// The entity's real name always shows, even with an identity-concealing item
/// worn (masks, helmets). Used for mobs that shouldn't be maskable, like the
/// security K9. Mirrors the built-in always-identifiable handling for borgs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AlwaysIdentifiableComponent : Component
{
}
