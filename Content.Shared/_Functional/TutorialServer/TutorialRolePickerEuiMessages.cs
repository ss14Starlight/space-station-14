using Robust.Shared.Serialization;
using Content.Shared.Eui;

namespace Content.Shared._Functional.TutorialServer;

[Serializable, NetSerializable]
public sealed class TutorialRolePickerEuiState : EuiStateBase
{
    public List<TutorialRolePickerEntry> Roles { get; }

    public TutorialRolePickerEuiState(List<TutorialRolePickerEntry> roles)
    {
        Roles = roles;
    }
}

[Serializable, NetSerializable]
public sealed class TutorialRolePickerEntry
{
    public string RoleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public bool Stub { get; set; }

    /// <summary>Sort key inside the category, lowest first. See TutorialRolePrototype.PickerOrder.</summary>
    public int Order { get; set; }

    /// <summary>Set when this player's species cannot take the role; shown greyed, refused if asked for.</summary>
    public bool BlockedForSpecies { get; set; }
}

[Serializable, NetSerializable]
public sealed class TutorialSelectRoleMessage : EuiMessageBase
{
    public string RoleId { get; set; } = string.Empty;
    public bool ConfirmedStub { get; set; }

    public TutorialSelectRoleMessage(string roleId, bool confirmedStub)
    {
        RoleId = roleId;
        ConfirmedStub = confirmedStub;
    }

    public TutorialSelectRoleMessage()
    {
    }
}

[Serializable, NetSerializable]
public sealed class TutorialQuitPickerMessage : EuiMessageBase;

[Serializable, NetSerializable]
public sealed class TutorialAcknowledgeStepMessage : EntityEventArgs
{
}
