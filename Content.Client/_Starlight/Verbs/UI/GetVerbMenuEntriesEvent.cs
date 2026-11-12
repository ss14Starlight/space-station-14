namespace Content.Client._Starlight.Verbs.UI;

/// <summary>
/// Raised on an entity when its verb menu is filled, to list extra entries below its interaction verbs.
/// </summary>
public sealed class GetVerbMenuEntriesEvent(EntityUid user) : EntityEventArgs
{
    public readonly EntityUid User = user;

    public readonly List<VerbMenuEntryElement> Entries = [];
}
