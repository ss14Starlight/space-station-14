using Content.Shared._Starlight.Access.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.StatusIcon;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Access.Components;


// A privileged/target dual-slot ID console like "Content.Shared.Access.Components.IdCardConsoleComponent" except it can only rewrite a target's job title and job icon.
// Cannot read/write access tags.

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedJobIdentityConsoleSystem))]
public sealed partial class JobIdentityConsoleComponent : Component
{
    public static string PrivilegedIdCardSlotId = "JobIdentityConsole-privilegedId";
    public static string TargetIdCardSlotId = "JobIdentityConsole-targetId";

    [DataField]
    public ItemSlot PrivilegedIdSlot = new();

    [DataField]
    public ItemSlot TargetIdSlot = new();

    // An icon is only accepted if its "JobIconPrototype.Tags" overlaps this set
    // An empty set rejects every icon

    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<TagPrototype>> RequiredTags = new();

    [Serializable, NetSerializable]
    public sealed class WriteJobIdentityMessage : BoundUserInterfaceMessage
    {
        public readonly string JobTitle;
        public readonly ProtoId<JobIconPrototype>? JobIcon;

        public WriteJobIdentityMessage(string jobTitle, ProtoId<JobIconPrototype>? jobIcon)
        {
            JobTitle = jobTitle;
            JobIcon = jobIcon;
        }
    }

    [Serializable, NetSerializable]
    public sealed class JobIdentityConsoleBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly bool IsPrivilegedIdPresent;
        public readonly bool IsPrivilegedIdAuthorized;
        public readonly bool IsTargetIdPresent;
        public readonly string PrivilegedIdName;
        public readonly string TargetIdName;
        public readonly string? TargetIdJobTitle;
        public readonly ProtoId<JobIconPrototype>? TargetIdJobIcon;

        public JobIdentityConsoleBoundUserInterfaceState(
            bool isPrivilegedIdPresent,
            bool isPrivilegedIdAuthorized,
            bool isTargetIdPresent,
            string privilegedIdName,
            string targetIdName,
            string? targetIdJobTitle,
            ProtoId<JobIconPrototype>? targetIdJobIcon)
        {
            IsPrivilegedIdPresent = isPrivilegedIdPresent;
            IsPrivilegedIdAuthorized = isPrivilegedIdAuthorized;
            IsTargetIdPresent = isTargetIdPresent;
            PrivilegedIdName = privilegedIdName;
            TargetIdName = targetIdName;
            TargetIdJobTitle = targetIdJobTitle;
            TargetIdJobIcon = targetIdJobIcon;
        }
    }

    [Serializable, NetSerializable]
    public enum JobIdentityConsoleUiKey : byte
    {
        Key,
    }
}
