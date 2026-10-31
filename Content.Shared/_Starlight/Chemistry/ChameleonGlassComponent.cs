using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Chemistry;

/// <summary>
/// Stores the chosen drink disguise until a previously emptied glass is refilled.
/// </summary>
[RegisterComponent]
public sealed partial class ChameleonGlassComponent : Component
{
    /// <summary>
    /// The reagent whose glass appearance is being imitated.
    /// </summary>
    [DataField]
    public string? SelectedReagent;

    /// <summary>
    /// Tracks whether a drink has been added since the current disguise was selected.
    /// </summary>
    public bool WasFilledSinceSelection;

    /// <summary>
    /// Clears the current disguise when a new drink is added after emptying.
    /// </summary>
    public bool ResetOnNextFill;

    /// <summary>
    /// The player authorized in the picker UI.
    /// </summary>
    public EntityUid? Editor;

    public EntityUid? Spoon;

    public string? PendingReagent;

    /// <summary>
    /// When an EMP should next scramble the disguise.
    /// </summary>
    public TimeSpan NextEmpChange;
}

[Serializable, NetSerializable]
public sealed class ChameleonGlassUiState(string? selectedReagent) : BoundUserInterfaceState
{
    public readonly string? SelectedReagent = selectedReagent;
}

[Serializable, NetSerializable]
public sealed class ChameleonGlassSelectMessage(string reagentId) : BoundUserInterfaceMessage
{
    public readonly string ReagentId = reagentId;
}

[Serializable, NetSerializable]
public enum ChameleonGlassUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum ChameleonGlassVisuals : byte
{
    Override
}

[Serializable, NetSerializable]
public sealed partial class ChameleonGlassStirDoAfterEvent : SimpleDoAfterEvent;
