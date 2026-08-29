using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Language.Components;

/// <summary>
///     Blocks language grants from sentience effects like Cognizine.
///     Does not affect ghost role eligibility.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LanguageGrantImmuneComponent : Component;
