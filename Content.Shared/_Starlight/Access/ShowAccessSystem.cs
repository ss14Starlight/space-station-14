using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Access;
using Content.Shared.Access.Components;
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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PdaComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsPda);
        SubscribeLocalEvent<AccessComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsAccess);
        SubscribeLocalEvent<InventoryComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbsInventory);
    }

    private void OnGetExamineVerbsPda(Entity<PdaComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
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
                if (TryComp(slot.ContainedEntity, out showAccess))
                    break;
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
        var localized = ProtoToLocalizedName(tags);

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString(showAccess.ExamineLocId, ("access", tags.Count > 0 ? string.Join(", ", localized) : "None")));

        _examine.AddDetailedExamineVerb(args, showAccess, msg, Loc.GetString("show-access-verb-text"), "/Textures/_Starlight/Interface/VerbIcons/examine-access.png", Loc.GetString("show-access-verb-message"));
    }

    private HashSet<string> ProtoToLocalizedName(HashSet<ProtoId<AccessLevelPrototype>> protoIds)
    {
        HashSet<string> localized = [];

        foreach (var protoId in protoIds)
        {
            if (!_proto.TryIndex(protoId, out var accessLevel)) continue;
            var name = accessLevel.Name ?? accessLevel.ID;
            localized.Add(Loc.GetString(name));
        }

        return localized;
    }
}
