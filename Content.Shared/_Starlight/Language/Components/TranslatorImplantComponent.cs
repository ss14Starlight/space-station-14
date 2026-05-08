using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Language.Components;

/// <summary>
///     An implant that allows the implantee to speak and understand other languages.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TranslatorImplantComponent : BaseTranslatorComponent
{
    /// <summary>
    ///     Whether the implantee knows the languages necessary to speak using this implant.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SpokenRequirementSatisfied;

    /// <summary>
    ///     Whether the implantee knows the languages necessary to understand translations of this implant.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UnderstoodRequirementSatisfied;
}
