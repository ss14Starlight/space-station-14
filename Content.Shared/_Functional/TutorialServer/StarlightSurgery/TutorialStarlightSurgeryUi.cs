using Robust.Shared.Serialization;

namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Bound UI key for the tutorial one-off Starlight-style surgery window.
/// Only entities with <see cref="TutorialStarlightSurgeryTargetComponent"/> expose this UI.
/// </summary>
[Serializable, NetSerializable]
public enum TutorialStarlightSurgeryUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class TutorialStarlightSurgeryBuiState : BoundUserInterfaceState
{
    /// <summary>
    /// Per virtual body-part id → available surgeries (id, optional suffix, completed).
    /// </summary>
    public required Dictionary<string, List<(string SurgeryId, string Suffix, bool IsCompleted)>> Choices { get; init; }

    public bool IsLyingDown { get; init; }
}

[Serializable, NetSerializable]
public sealed class TutorialStarlightSurgeryStepChosenBuiMsg : BoundUserInterfaceMessage
{
    public required string Part { get; init; }
    public required string Surgery { get; init; }
    public required string Step { get; init; }
}
