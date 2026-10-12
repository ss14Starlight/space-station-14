using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

/// <summary>
/// Stores the server-authoritative annotations made through a tactical planning console.
/// </summary>
[RegisterComponent]
public sealed partial class TacticalPlanningConsoleComponent : Component
{
    [DataField]
    public int MaxAnnotations = 100;

    [DataField]
    public int MaxTotalPoints = 4096;

    public readonly List<TacticalPlanAnnotation> Annotations = new();
    // A new token invalidates client caches after a round/server change, even if an entity
    // receives the same network ID. Limits and bookkeeping are per console, not global.
    public readonly string CacheToken = Guid.NewGuid().ToString("N");
    public int TotalPoints;
    // A stroke can exceed the remaining point budget before TotalPoints reaches the cap.
    // Latch "full" in that case so players can use the Reset button.
    public bool CapacityReached;
    public readonly Dictionary<EntityUid, Color> PlayerColors = new();
    public readonly Dictionary<EntityUid, TimeSpan> LastSubmission = new();
    public readonly Dictionary<EntityUid, TimeSpan> LastRejectionNotice = new();
    public readonly HashSet<EntityUid> Viewers = new();
    public float SubmissionTokens = 5f;
    public TimeSpan LastBudgetUpdate;
    public int NextAnnotationId;
}

[Serializable, NetSerializable]
public enum TacticalPlanningConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// A persistent map annotation. The author is recorded server-side so every viewer can inspect it.
/// </summary>
[Serializable, NetSerializable]
public sealed class TacticalPlanAnnotation(int id, TacticalPlanAnnotationKind kind, Color color, List<Vector2> points, string author)
{
    public readonly int Id = id;
    public readonly TacticalPlanAnnotationKind Kind = kind;
    public readonly Color Color = color;
    public readonly List<Vector2> Points = points;
    public readonly string Author = author;
}

[Serializable, NetSerializable]
public enum TacticalPlanAnnotationKind : byte
{
    Stroke,
    Dot,
}

/// <summary>
/// Requests only changes since the client's cached IDs when its token still matches.
/// The server may instead send a full snapshot if the cache is stale.
/// </summary>
[Serializable, NetSerializable]
public sealed class TacticalPlanRequestSnapshotMessage(string cacheToken, List<int> knownIds) : BoundUserInterfaceMessage
{
    public readonly string CacheToken = cacheToken;
    public readonly List<int> KnownIds = knownIds;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanSnapshotMessage(string cacheToken, bool full, List<TacticalPlanAnnotation> annotations,
    List<int> removedIds, Color assignedColor, bool isFull, bool syndicatePalette) : BoundUserInterfaceMessage
{
    public readonly string CacheToken = cacheToken;
    public readonly bool Full = full;
    public readonly List<TacticalPlanAnnotation> Annotations = annotations;
    public readonly List<int> RemovedIds = removedIds;
    public readonly Color AssignedColor = assignedColor;
    public readonly bool IsFull = isFull;
    public readonly bool SyndicatePalette = syndicatePalette;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanAddAnnotationMessage(TacticalPlanAnnotationKind kind, List<Vector2> points, Color color) : BoundUserInterfaceMessage
{
    public readonly TacticalPlanAnnotationKind Kind = kind;
    public readonly List<Vector2> Points = points;
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanDrawingRejectedMessage(string reason) : BoundUserInterfaceMessage
{
    public readonly string Reason = reason;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanCapacityReachedMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TacticalPlanAnnotationAddedMessage(TacticalPlanAnnotation annotation, bool isFull) : BoundUserInterfaceMessage
{
    public readonly TacticalPlanAnnotation Annotation = annotation;
    public readonly bool IsFull = isFull;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanAnnotationErasedMessage(int id, bool isFull) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
    public readonly bool IsFull = isFull;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanEraseAnnotationMessage(int id) : BoundUserInterfaceMessage
{
    public readonly int Id = id;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanResetMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class TacticalPlanClearedMessage : BoundUserInterfaceMessage;
