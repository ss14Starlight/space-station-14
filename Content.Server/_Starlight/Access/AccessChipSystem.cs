using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Server.Popups;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Access;

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

        // Try apply accesses and capture result reason
        var result = TryAddAccessesToCard(args.Target.Value, chip.Comp);

        var accessNames = new List<string>();
        foreach (var accessId in chip.Comp.GrantedAccesses)
        {
            if (_prototypeManager.Resolve(accessId, out var proto))
            {
                accessNames.Add(proto.GetAccessLevelName());
            }
        }

        // Nothing changed because access already existed
        if (result == AccessChipResult.AlreadyHasAllAccess)
        {
            _popup.PopupCursor(
                Loc.GetString("access-chip-already-has-access", ("access", string.Join(", ", accessNames))),
                args.User,
                PopupType.MediumCaution);
            return;
        }

        // Hard failure (no access component or system failure)
        if (result != AccessChipResult.Success)
            return;

        args.Handled = true;

        _popup.PopupCursor(
            Loc.GetString("access-chip-used", ("access", string.Join(", ", accessNames))),
            args.User,
            PopupType.Medium);

        QueueDel(chip.Owner);
    }

    private enum AccessChipResult
    {
        Success,
        AlreadyHasAllAccess,
        Failure
    }

    private AccessChipResult TryAddAccessesToCard(EntityUid target, AccessChipComponent chipComponent)
    {
        if (!TryComp<AccessComponent>(target, out var accessComp))
            return AccessChipResult.Failure;

        var hasAtLeastOneNew = false;

        foreach (var access in chipComponent.GrantedAccesses)
        {
            if (!accessComp.Tags.Contains(access))
            {
                hasAtLeastOneNew = true;
                break;
            }
        }

        // ID card already has access
        if (!hasAtLeastOneNew)
            return AccessChipResult.AlreadyHasAllAccess;

        var newAccesses = new List<ProtoId<AccessLevelPrototype>>(accessComp.Tags);
        newAccesses.AddRange(chipComponent.GrantedAccesses);

        if (!_access.TrySetTags(target, newAccesses))
            return AccessChipResult.Failure;

        return AccessChipResult.Success;
    }
}
