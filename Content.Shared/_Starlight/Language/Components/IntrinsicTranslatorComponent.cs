using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Language.Components;

/// <summary>
///   A translator attached to an entity that translates its speech.
///   An example is a translator implant that allows the speaker to speak another Language.
/// </summary>
[Virtual]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public partial class IntrinsicTranslatorComponent : BaseTranslatorComponent;
