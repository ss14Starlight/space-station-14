using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Language.Components;

public abstract partial class BaseTranslatorComponent : Component
{
    /// <summary>
    ///   The list of additional languages this translator allows the wielder to speak.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<LanguagePrototype>> Spoken = new();

    /// <summary>
    ///   The list of additional languages this translator allows the wielder to understand.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<LanguagePrototype>> Understood = new();

    /// <summary>
    ///   The languages the wielding MUST know in order for this translator to have effect.
    ///   The field <see cref="RequiresAll"/> indicates whether all of them are required, or just one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<LanguagePrototype>> Requires = new();

    /// <summary>
    ///   If true, the wielder must understand all languages in <see cref="Requires"/> to speak <see cref="Spoken"/>,
    ///   and understand all languages in <see cref="Requires"/> to understand <see cref="Understood"/>.
    ///
    ///   Otherwise, at least one Language must be known (or the list must be empty).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RequiresAll;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
