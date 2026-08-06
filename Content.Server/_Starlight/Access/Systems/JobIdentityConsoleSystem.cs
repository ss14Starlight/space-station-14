using Content.Server.Access.Systems;
using Content.Shared._Starlight.Access.Components;
using Content.Shared._Starlight.Access.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.StatusIcon;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using static Content.Shared._Starlight.Access.Components.JobIdentityConsoleComponent;

namespace Content.Server._Starlight.Access.Systems;

//  Handles JobIdentityConsoleComponent- (modeled on the base ID card console) that can only modify a target's job title/hud icon.
[UsedImplicitly]
public sealed partial class JobIdentityConsoleSystem : SharedJobIdentityConsoleSystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UserInterfaceSystem _userInterface = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobIdentityConsoleComponent, WriteJobIdentityMessage>(OnWriteJobIdentityMessage);

        SubscribeLocalEvent<JobIdentityConsoleComponent, ComponentStartup>(UpdateUserInterface);
        SubscribeLocalEvent<JobIdentityConsoleComponent, EntInsertedIntoContainerMessage>(UpdateUserInterface);
        SubscribeLocalEvent<JobIdentityConsoleComponent, EntRemovedFromContainerMessage>(UpdateUserInterface);
    }

    private void OnWriteJobIdentityMessage(EntityUid uid, JobIdentityConsoleComponent component, WriteJobIdentityMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        TryWriteJobIdentity(uid, component, args.JobTitle, args.JobIcon, player);

        UpdateUserInterface(uid, component, args);
    }

    private void TryWriteJobIdentity(EntityUid uid,
        JobIdentityConsoleComponent component,
        string newJobTitle,
        ProtoId<JobIconPrototype>? newJobIconId,
        EntityUid player)
    {
        if (component.TargetIdSlot.Item is not { Valid: true } targetId || !PrivilegedIdIsAuthorized(uid, component))
            return;

        // When a null icon is returned the current icon is kept - used to prevent issues when only a name change occurs.
        JobIconPrototype? newJobIcon = null;
        if (newJobIconId != null)
        {
            if (!_prototype.Resolve(newJobIconId, out newJobIcon))
                return;

            // Prevents applying icons not accessible on the console.
            if (!newJobIcon.Tags.Overlaps(component.RequiredTags))
                return;
        }

        _idCard.TryChangeJobTitle(targetId, newJobTitle, player: player);
        if (newJobIcon != null)
            _idCard.TryChangeJobIcon(targetId, newJobIcon, player: player);

        _adminLogger.Add(LogType.Action,
            $"{player} used {ToPrettyString(uid)} to set {ToPrettyString(targetId)}'s job title to \"{newJobTitle}\"{(newJobIcon != null ? $" and job icon to {newJobIconId}" : string.Empty)}");
    }

    private void UpdateUserInterface(EntityUid uid, JobIdentityConsoleComponent component, EntityEventArgs args)
    {
        if (!component.Initialized)
            return;

        var privilegedIdName = string.Empty;
        if (component.PrivilegedIdSlot.Item is { Valid: true } privilegedId)
            privilegedIdName = Comp<MetaDataComponent>(privilegedId).EntityName;

        var targetIdName = string.Empty;
        string? targetJobTitle = null;
        ProtoId<JobIconPrototype>? targetJobIcon = null;

        if (component.TargetIdSlot.Item is { Valid: true } targetId)
        {
            targetIdName = Name(targetId);
            var targetIdComponent = Comp<IdCardComponent>(targetId);
            targetJobTitle = targetIdComponent.LocalizedJobTitle;
            targetJobIcon = targetIdComponent.JobIcon;
        }

        var newState = new JobIdentityConsoleBoundUserInterfaceState(
            component.PrivilegedIdSlot.HasItem,
            PrivilegedIdIsAuthorized(uid, component),
            component.TargetIdSlot.HasItem,
            privilegedIdName,
            targetIdName,
            targetJobTitle,
            targetJobIcon);

        _userInterface.SetUiState(uid, JobIdentityConsoleUiKey.Key, newState);
    }

    /// Returns true if there is an ID in "JobIdentityConsoleComponent.PrivilegedIdSlot" and if there's "AccessReaderComponent" that the id satisfies that component.
    private bool PrivilegedIdIsAuthorized(EntityUid uid, JobIdentityConsoleComponent component)
    {
        if (component.PrivilegedIdSlot.Item is not { Valid: true } id)
            return false;

        if (!TryComp<AccessReaderComponent>(uid, out var reader))
            return true;

        return _accessReader.IsAllowed(id, uid, reader);
    }
}
