using Content.Shared.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Access.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedIdCardConsoleSystem))]
public sealed partial class IdCardConsoleComponent : Component
{
    public static string PrivilegedIdCardSlotId = "IdCardConsole-privilegedId";
    public static string TargetIdCardSlotId = "IdCardConsole-targetId";

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();

    [DataField]
    public ItemSlot TargetIdSlot = new();

    // Starlight-edit: If "AllIconsUnlocked" is true a given console can see and assign all hud icons.
    // Enabled by default on the Universal ID Console, and can be enabled by a player when using an EMAG on an ID card computer.
    [DataField]
    public bool AllIconsUnlocked = false;

    // Starlight-edit: An icon is only accepted for selection if its "JobIconPrototype.Tags" is set unless AllIconsUnlocked is set or the console has been emagged. Defaults to the standard crew icon tag.
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TagPrototype>> RequiredTags = new() { SharedIdCardConsoleSystem.CrewJobIconTag };

    [Serializable, NetSerializable]
    public sealed class WriteToTargetIdMessage : BoundUserInterfaceMessage
    {
        public readonly string FullName;
        public readonly string JobTitle;
        public readonly List<ProtoId<AccessLevelPrototype>> AccessList;
        public readonly ProtoId<JobPrototype>? JobPrototype; // Starlight: Nullable
        public readonly ProtoId<JobIconPrototype>? JobIcon; // Starlight-edit

        public WriteToTargetIdMessage(string fullName, string jobTitle, List<ProtoId<AccessLevelPrototype>> accessList, ProtoId<JobPrototype>? jobPrototype, ProtoId<JobIconPrototype>? jobIcon) // Starlight: Nullable jobPrototype, jobIcon
        {
            FullName = fullName;
            JobTitle = jobTitle;
            AccessList = accessList;
            JobPrototype = jobPrototype;
            JobIcon = jobIcon;
        }
    }
    // Starlight-edit: Start
    [Serializable, NetSerializable]
    public sealed class AccessGroupSelectedMessage : BoundUserInterfaceMessage
    {
        public readonly ProtoId<AccessGroupPrototype> SelectedGroup;

        public AccessGroupSelectedMessage(ProtoId<AccessGroupPrototype> selectedGroup)
        {
            SelectedGroup = selectedGroup;
        }
    }
    // Starlight-edit: End

    // Put this on shared so we just send the state once in PVS range rather than every time the UI updates.

    [DataField, AutoNetworkedField]
    // Starlight-edit: Start
    public List<ProtoId<AccessGroupPrototype>> AccessGroups = new();
    [AutoNetworkedField]
    public ProtoId<AccessGroupPrototype>? CurrentAccessGroup;
    // Starlight-edit: End

    [Serializable, NetSerializable]
    public sealed class IdCardConsoleBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string PrivilegedIdName;
        public readonly bool IsPrivilegedIdPresent;
        public readonly bool IsPrivilegedIdAuthorized;
        public readonly bool IsTargetIdPresent;
        public readonly string TargetIdName;
        public readonly string? TargetIdFullName;
        public readonly string? TargetIdJobTitle;
        public readonly List<ProtoId<AccessLevelPrototype>>? TargetIdAccessList;
        public readonly List<ProtoId<AccessLevelPrototype>>? AllowedModifyAccessList;
        public readonly ProtoId<JobPrototype> TargetIdJobPrototype;
        // Starlight-edit: Start
        public readonly ProtoId<AccessGroupPrototype> CurrentAccessGroup;
        public readonly List<ProtoId<AccessGroupPrototype>>? AvailableAccessGroups;
        public readonly ProtoId<JobIconPrototype>? TargetIdJobIcon;
        // True if every job icon (not just those tagged for that console) is unlocked for selection,
        // because "IdCardConsoleComponent.AllIconsUnlocked" is set or the console has been emagged.
        public readonly bool AllIconsUnlocked;
        // Starlight-edit: End

        public IdCardConsoleBoundUserInterfaceState(bool isPrivilegedIdPresent,
            bool isPrivilegedIdAuthorized,
            bool isTargetIdPresent,
            string? targetIdFullName,
            string? targetIdJobTitle,
            List<ProtoId<AccessLevelPrototype>>? targetIdAccessList,
            List<ProtoId<AccessLevelPrototype>>? allowedModifyAccessList,
            ProtoId<JobPrototype> targetIdJobPrototype,
            string privilegedIdName,
            string targetIdName,
            // Starlight-edit: Start
            ProtoId<AccessGroupPrototype> currentAccessGroup,
            List<ProtoId<AccessGroupPrototype>>? availableAccessGroups = null,
            ProtoId<JobIconPrototype>? targetIdJobIcon = null,
            bool allIconsUnlocked = false)
            // Starlight-edit: End
        {
            IsPrivilegedIdPresent = isPrivilegedIdPresent;
            IsPrivilegedIdAuthorized = isPrivilegedIdAuthorized;
            IsTargetIdPresent = isTargetIdPresent;
            TargetIdFullName = targetIdFullName;
            TargetIdJobTitle = targetIdJobTitle;
            TargetIdAccessList = targetIdAccessList;
            AllowedModifyAccessList = allowedModifyAccessList;
            TargetIdJobPrototype = targetIdJobPrototype;
            PrivilegedIdName = privilegedIdName;
            TargetIdName = targetIdName;
            // Starlight-edit: Start
            CurrentAccessGroup = currentAccessGroup;
            AvailableAccessGroups = availableAccessGroups;
            TargetIdJobIcon = targetIdJobIcon;
            AllIconsUnlocked = allIconsUnlocked;
            // Starlight-edit: End
        }
    }

    [Serializable, NetSerializable]
    public enum IdCardConsoleUiKey : byte
    {
        Key,
    }
}
