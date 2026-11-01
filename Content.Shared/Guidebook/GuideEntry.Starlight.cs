using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Guidebook;

/// <summary>
///     Starlight search and navigation color metadata for guidebook entries.
/// </summary>
public partial class GuideEntry
{
    /// <summary>
    ///     Additional localized search terms, alongside the automatic
    ///     <c>guidebook-search-keywords-{lowercase Id}</c> localization entry.
    /// </summary>
    [DataField]
    public List<LocId> SearchKeywords = new();

    /// <summary>
    ///     Color of this entry and its descendants in the table of contents, unless a child overrides it.
    /// </summary>
    [DataField]
    public Color? Color;

    /// <summary>
    ///     Department whose existing color this guide uses, unless <see cref="Color"/> is explicitly set.
    /// </summary>
    [DataField]
    public ProtoId<DepartmentPrototype>? Department;
}
