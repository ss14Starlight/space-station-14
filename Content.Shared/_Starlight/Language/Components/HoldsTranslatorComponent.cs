using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Language.Components;

/// <summary>
///     Applied internally to the holder of an Entity with <see cref="HandheldTranslatorComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HoldsTranslatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Translators = new();
}
