using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Traits.Assorted;

/// <summary>
///     When applied to a not-yet-spawned player entity, removes the species specific language from the lists of their languages
/// </summary>
[RegisterComponent]
public sealed partial class XenosocializedTraitComponent : Component
{
    /// <summary>
    ///     The language added by being a Neocyte.
    ///     This language is ignored when checking for base languages, unless no other languages could be found.
    /// </summary>
    [DataField]
    public ProtoId<LanguagePrototype> NeocyteLanguage = "Machine";
}
// Derived from ForeignerTraitComponent.cs
