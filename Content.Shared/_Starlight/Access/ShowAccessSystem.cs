using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.PDA;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Access;

public sealed partial class ShowAccessSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    // ReSharper disable once UseCollectionExpression | Fucking clientside sandboxing
    private static readonly List<ProtoId<AccessGroupPrototype>> _blacklistedGroups = new(){"AllAccess", "CyborgAllAccess", "Armory"};
    private const string CommandProtoId = "Command";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsPda);
        SubscribeLocalEvent<AccessComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsAccess);
        SubscribeLocalEvent<InventoryComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsInventory);
    }

    private void OnGetExamineVerbsPda(Entity<PdaComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (args.Verbs.Any(verb => verb.Text == Loc.GetString("show-access-verb-text")))
            return;

        if (!TryGetShowAccess(args.User, out var showAccess))
            return;

        if (showAccess.ItemsOnly && !HasComp<ItemComponent>(ent))
            return;

        if (!TryComp<AccessComponent>(ent.Comp.ContainedId, out var access))
            return;

        AddVerb(showAccess, args, access.Tags);
    }

    private void OnGetExamineVerbsAccess(Entity<AccessComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (args.Verbs.Any(verb => verb.Text == Loc.GetString("show-access-verb-text")))
            return;

        if (!TryGetShowAccess(args.User, out var showAccess))
            return;

        if (showAccess.ItemsOnly && !HasComp<ItemComponent>(ent))
            return;

        AddVerb(showAccess, args, ent.Comp.Tags);
    }

    private void OnGetExamineVerbsInventory(Entity<InventoryComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (args.Verbs.Any(verb => verb.Text == Loc.GetString("show-access-verb-text")))
            return;

        if (!TryGetShowAccess(args.User, out var showAccess))
            return;

        TryComp<AccessComponent>(ent, out var access);

        if (!showAccess.SeeIdHolder && access is null)
            return;

        if (showAccess.ItemsOnly && !HasComp<ItemComponent>(ent))
            return;

        var combinedAccess = new HashSet<ProtoId<AccessLevelPrototype>>();
        if (access is not null)
            combinedAccess.UnionWith(access.Tags);

        // Get access from any slot flagged with IDCARD.
        if (_inventory.TryGetContainerSlotEnumerator((ent, ent.Comp), out var slots, SlotFlags.IDCARD))
            while (slots.MoveNext(out var slot))
                if (TryGetAccessComp(slot.ContainedEntity, out access))
                    combinedAccess.UnionWith(access.Tags);

        // Get access from anything in hands.
        if (TryComp<HandsComponent>(ent, out var hands))
            using (var handEnumerator = _hands.EnumerateHeld((ent, hands)).GetEnumerator())
                while (handEnumerator.MoveNext())
                    if (TryGetAccessComp(handEnumerator.Current, out access))
                        combinedAccess.UnionWith(access.Tags);


        AddVerb(showAccess, args, combinedAccess);
    }

    private bool TryGetShowAccess(EntityUid uid, [NotNullWhen(true)] out ShowAccessComponent? showAccess)
    {
        if (TryComp(uid, out showAccess)) return true;
        if (TryComp<InventoryComponent>(uid, out var inventory))
        {
            foreach (var slot in inventory.Containers)
            {
                if (slot.ContainedEntity is null) continue;
                if (!_inventory.TryGetContainingSlot(
                        (slot.ContainedEntity.Value, Transform(slot.ContainedEntity.Value),
                            MetaData(slot.ContainedEntity.Value)), out var slotDef)) continue;
                if (!TryComp<ClothingComponent>(slot.ContainedEntity, out var clothing)) continue;
                if ((clothing.Slots & slotDef.SlotFlags) == 0x00) continue;
                if (TryComp(slot.ContainedEntity, out showAccess))
                    break;
            }
            if (showAccess is null) return false;
        }
        else return false;
        return true;
    }

    private bool TryGetAccessComp(EntityUid? uid, [NotNullWhen(true)] out AccessComponent? access)
    {
        if (TryComp(uid, out access))
            return true;

        if (TryComp<PdaComponent>(uid, out var pda))
            return TryComp(pda.ContainedId, out access);

        return false;
    }

    private void AddVerb(ShowAccessComponent showAccess, GetVerbsEvent<ExamineVerb> args, HashSet<ProtoId<AccessLevelPrototype>> tags)
    {
        var localized = LocalizeAndSort(tags);

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString(showAccess.ExamineLocId, ("groups", tags.Count > 0 ? string.Join("\n", localized) : "None")));

        _examine.AddDetailedExamineVerb(args, showAccess, msg, Loc.GetString("show-access-verb-text"), "/Textures/_Starlight/Interface/VerbIcons/examine-access.png", Loc.GetString("show-access-verb-message"));
    }

    private List<string> LocalizeAndSort(HashSet<ProtoId<AccessLevelPrototype>> protoIds)
    {
        // Get groups then sort alphabetically to stay organized but also to make sure the order is same on client+server.
        var groups = _proto.EnumeratePrototypes<AccessGroupPrototype>()
            .Where(group => !_blacklistedGroups.Contains(group.ID)).ToList();
        groups = groups.OrderBy(group => group.Name ?? group.ID).ToList();

        /*
         * Move command if present to top of list since despite that not being alphabetical order it's arguably the most important.
         * Also because cargo comes before command in the above list so Quartermaster would be separated if I don't do this.
         */
        foreach (var group in groups.ToList().Where(group => group.ID == CommandProtoId))
        {
            groups.Remove(group);
            groups.Insert(0, group);
            break;
        }

        /*
         * This is probably stupid and I probably need to implement some sort of priority
         * property on access groups but here we just put tags into a list with the group association and
         * ignore duplicates. Mostly works but could be an issue eventually, shrug.
         */
        var grouped = new Dictionary<ProtoId<AccessLevelPrototype>, string>();
        foreach (var group in groups)
        {
            var name = group.Name ?? group.ID;
            foreach (var tag in group.Tags) grouped.TryAdd(tag, name);
        }

        // alphabetically sort the actual access tags into their sorted groups.
        var sorted = new Dictionary<string, SortedSet<string>>();
        const string Ungrouped = "Ungrouped";
        foreach (var protoId in protoIds)
        {
            if (!_proto.TryIndex(protoId, out var proto))
                continue;
            var name = Loc.GetString("show-access-examined-access", ("access", Loc.GetString(proto.Name ?? proto.ID)));
            var group = grouped.GetValueOrDefault(protoId, Ungrouped);
            if (!sorted.TryGetValue(group, out var list))
            {
                list = [];
                sorted[group] = list;
            }
            list.Add(name);
        }

        // now just grab all the names of the access tags and shove them into a single string list (and also move ungrouped to the back)
        var result = new List<string>();
        foreach (var group in sorted.Keys
                     .Where(g => g != Ungrouped)
                     .OrderBy(g => g)
                     .Append(Ungrouped))
        {
            if (!sorted.TryGetValue(group, out var accessList) || accessList.Count == 0)
                continue;

            result.Add(Loc.GetString("show-access-examined-group",
                ("group", group),
                ("accesses", string.Join(", ", accessList))));
        }

        // voilà this code sucks here's your ordered access tags
        return result;
    }
}
