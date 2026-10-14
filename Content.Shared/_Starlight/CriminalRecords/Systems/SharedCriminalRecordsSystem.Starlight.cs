using Content.Shared.IdentityManagement;
using Content.Shared.NameModifier.EntitySystems;

// ReSharper disable once CheckNamespace
namespace Content.Shared.CriminalRecords.Systems;

public abstract partial class SharedCriminalRecordsSystem
{
    [Dependency] private NameModifierSystem _nameModifier = default!;

    /// <summary>
    /// The name to match against records. Records don't include name modifiers like a name identifier's "(739)",
    /// so an entity showing its true identity uses its base name; a disguised one uses its identity name.
    /// </summary>
    public string GetRecordName(EntityUid uid)
    {
        var name = Identity.Name(uid, EntityManager);
        if (name == Name(uid))
            return _nameModifier.GetBaseName(uid);

        return name;
    }
}
