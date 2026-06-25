using Content.Server.Popups;
using Content.Shared._Starlight.Access;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Access;

/// <summary>
/// System for handling single-use access chips that grant accesses to ID cards.
/// </summary>
public sealed partial class AccessChipSystem : EntitySystem
{
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AccessChipComponent, AfterInteractEvent>(OnChipAfterInteract);
    }

    private void OnChipAfterInteract(Entity<AccessChipComponent> chip, ref AfterInteractEvent args)
    {
        if (args.Target == null || !args.CanReach || args.Handled)
            return;

        if (!TryComp<IdCardComponent>(args.Target, out var _))
        {
            _popup.PopupCursor(
                Loc.GetString("access-chip-not-id-card"),
                args.User,
                PopupType.MediumCaution);
            return;
        }

        // Attempt to apply accesses and retrieve the valid accesses for popup displays.
        var result = TryAddAccessesToCard(
            args.Target.Value,
            chip.Comp,
            out var validAccesses);

        // Hard failure (no access component, no valid accesses, or set failure).
        if (result == AccessChipResult.Failure)
            return;

        // Build display names only for accesses that were actually considered valid.
        var accessNames = new List<string>();
        foreach (var accessId in validAccesses)
        {
            if (_prototypeManager.Resolve(accessId, out var proto))
                accessNames.Add(proto.GetAccessLevelName());
        }

        // Nothing changed because all valid accesses already existed on the ID.
        if (result == AccessChipResult.AlreadyHasAllAccess)
        {
            _popup.PopupCursor(
                Loc.GetString("access-chip-already-has-access",
                    ("access", string.Join(", ", accessNames))),
                args.User,
                PopupType.MediumCaution);

            return;
        }

        args.Handled = true;

        _popup.PopupCursor(
            Loc.GetString("access-chip-used",
                ("access", string.Join(", ", accessNames))),
            args.User,
            PopupType.Medium);

        // Delete the chip after successful use.
        QueueDel(chip.Owner);
    }

    private enum AccessChipResult
    {
        Success,
        AlreadyHasAllAccess,
        Failure
    }

    /// <summary>
    /// Attempts to apply valid access levels from an access chip to an ID card.
    /// </summary>
    /// <param name="target">The ID card receiving the accesses.</param>
    /// <param name="chipComponent">The access chip being consumed.</param>
    /// <param name="validAccesses">
    /// The subset of chip accesses that resolve to valid prototypes and are allowed
    /// to be assigned to ID cards.
    /// </param>
    /// <returns>
    /// <see cref="AccessChipResult.Success"/> if at least one new access was granted,
    /// <see cref="AccessChipResult.AlreadyHasAllAccess"/> if all valid accesses were
    /// already present on the card, or
    /// <see cref="AccessChipResult.Failure"/> if the operation could not be completed.
    /// </returns>
    private AccessChipResult TryAddAccessesToCard(EntityUid target, AccessChipComponent chipComponent, out List<ProtoId<AccessLevelPrototype>> validAccesses)
    {
        validAccesses = new();

        if (!TryComp<AccessComponent>(target, out var accessComp))
            return AccessChipResult.Failure;

        // Build a sanitized list of accesses that are actually allowed on ID cards.
        foreach (var access in chipComponent.GrantedAccesses)
        {
            if (!_prototypeManager.Resolve(access, out var proto))
                continue;

            if (!proto.CanAddToIdCard)
                continue;

            validAccesses.Add(access);
        }

        // Nothing valid to apply.
        if (validAccesses.Count == 0)
            return AccessChipResult.Failure;

        var hasAtLeastOneNew = false;

        foreach (var access in validAccesses)
        {
            if (!accessComp.Tags.Contains(access))
            {
                hasAtLeastOneNew = true;
                break;
            }
        }

        // Card already contains every valid access from this chip.
        if (!hasAtLeastOneNew)
            return AccessChipResult.AlreadyHasAllAccess;

        // Merge existing accesses with new valid accesses, avoiding duplicates.
        var newAccesses = new List<ProtoId<AccessLevelPrototype>>(accessComp.Tags);

        foreach (var access in validAccesses)
        {
            if (!newAccesses.Contains(access))
                newAccesses.Add(access);
        }

        if (!_access.TrySetTags(target, newAccesses))
            return AccessChipResult.Failure;

        return AccessChipResult.Success;
    }
}
