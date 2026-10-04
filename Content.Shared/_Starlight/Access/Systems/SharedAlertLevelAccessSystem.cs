using System.Linq;
using Content.Shared._Starlight.Access.Components;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.Localizations;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Access.Systems;

[UsedImplicitly]
public abstract partial class SharedAlertLevelAccessSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    [SubscribeLocalEvent]
    private void OnExamine(Entity<AlertLevelAccessComponent> ent, ref ExaminedEvent ev)
    {
        if (ent.Comp.Level == null)
            return;

        using (ev.PushGroup(nameof(SharedAlertLevelAccessSystem), 10))
        {
            var level = Loc.GetString($"alert-level-{ent.Comp.Level}");
            var alert = Loc.GetString("alert-level-access-component-alert",
                ("level", level),
                ("color", ent.Comp.LevelColor));

            if (ent.Comp.AddedAccesses.TryGetValue(ent.Comp.Level, out var addedAccesses))
                FormatExamineAccessList(true, alert, addedAccesses, ref ev);
            if (ent.Comp.RemovedAccesses.TryGetValue(ent.Comp.Level, out var removedAccesses))
                FormatExamineAccessList(false, alert, removedAccesses, ref ev);
        }
    }

    private void FormatExamineAccessList(bool granted, string alert,
        List<HashSet<ProtoId<AccessLevelPrototype>>> accessLists,
        ref ExaminedEvent ev)
    {
        // For the examine text, the concept of "access lists" does not exist. The user only sees the individual accesses.
        // As such, we flatten the access lists into one access list.
        var accessList = accessLists.SelectMany(set => set).ToHashSet();

        var localizedCurrentNames = accessList.Select(access =>
        {
            var name = Loc.GetString("alert-level-access-component-unknown-id");
            if (_proto.Resolve(access, out var accessProto) && !string.IsNullOrWhiteSpace(accessProto.Name))
                name = Loc.GetString(accessProto.Name);
            return Loc.GetString("alert-level-access-component-access-label", ("access", name));
        }).ToList();
        var accessesFormatted = ContentLocalizationManager.FormatList(localizedCurrentNames);

        ev.PushMarkup(Loc.GetString($"alert-level-access-component-on-examine-{(granted ? "granted" : "revoked")}",
            ("alert", alert),
            ("accesses", accessesFormatted)));
    }

    /// <summary>
    /// When an access reader performs an access check, it fires an event that lets us modify the access lists prior
    /// to it checking whether an access-haver is allowed through.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGetAccessReaderAccessListsEvent(Entity<AlertLevelAccessComponent> ent,
        ref GetAccessReaderAccessListsEvent ev)
    {
        var xform = Transform(ent);

        // If not anchored or no level set, we don't need to do anything.
        if (!xform.Anchored || ent.Comp.Level == null)
            return;

        var toAdd = ent.Comp.AddedAccesses.GetValueOrDefault(ent.Comp.Level);
        var toRemove = ent.Comp.RemovedAccesses.GetValueOrDefault(ent.Comp.Level);

        // Apply added access lists.
        if (toAdd != null)
            foreach (var accessList in toAdd)
                ev.AccessLists.Add(accessList);

        // Apply removed access lists.
        if (toRemove != null)
            foreach (var removedAccessList in toRemove)
                ev.AccessLists.RemoveAll(accessList => accessList.SetEquals(removedAccessList));
    }
}
