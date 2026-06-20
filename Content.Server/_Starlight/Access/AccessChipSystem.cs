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
        // Ensure interaction was successful and can reach
        if (args.Target == null || !args.CanReach || args.Handled)
            return;

        // Check if target has IdCardComponent
        if (!TryComp<IdCardComponent>(args.Target, out var _))
        {
            _popup.PopupCursor(
                Loc.GetString("access-chip-not-id-card"),
                args.User,
                PopupType.MediumCaution);
            return;
        }

        // Attempt to add the accesses to the ID card
        if (!TryAddAccessesToCard(args.Target.Value, chip.Comp))
            return;

        args.Handled = true;

        // Show success popup with access names
        var accessNames = new List<string>();
        foreach (var accessId in chip.Comp.GrantedAccesses)
        {
            if (_prototypeManager.Resolve(accessId, out var proto))
            {
                accessNames.Add(proto.GetAccessLevelName());
            }
        }

        _popup.PopupCursor(
            Loc.GetString("access-chip-used", ("access", string.Join(", ", accessNames))),
            args.User,
            PopupType.Medium);

        // Delete the chip after successful use
        QueueDel(chip.Owner);
    }

    private bool TryAddAccessesToCard(EntityUid target, AccessChipComponent chipComponent)
    {
        // Get current accesses from the target ID card
        if (!TryComp<AccessComponent>(target, out var accessComp))
            return false;

        // Create a new set with existing accesses and new ones
        var newAccesses = new List<ProtoId<AccessLevelPrototype>>(accessComp.Tags);
        newAccesses.AddRange(chipComponent.GrantedAccesses);

        // Apply the new access set
        if (!_access.TrySetTags(target, newAccesses))
            return false;

        return true;
    }
}
