using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Computers.TacticalPlanning;

/// <summary>
/// Stores the server-authoritative annotations made through a tactical planning console.
/// </summary>
[RegisterComponent]
public sealed partial class TacticalPlanningConsoleComponent : Component
{
    /// <summary>Maximum number of annotations retained by this console.</summary>
    [DataField]
    public int MaxAnnotations = 100;

    /// <summary>Maximum combined number of points across its annotations.</summary>
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

[Serializable, NetSerializable]
public sealed class TacticalPlanAnnotation(int id, TacticalPlanAnnotationKind kind, Color color, List<Vector2> points, string author,
    TacticalPlanIcon icon = TacticalPlanIcon.None)
{
    public readonly int Id = id;
    public readonly TacticalPlanAnnotationKind Kind = kind;
    public readonly Color Color = color;
    public readonly List<Vector2> Points = points;
    public readonly string Author = author;
    public readonly TacticalPlanIcon Icon = icon;
}

[Serializable, NetSerializable]
public enum TacticalPlanAnnotationKind : byte
{
    Stroke,
    Dot,
    Icon,
}

/// <summary>Use fixed symbols instead of client-supplied sprite paths so icon requests can be validated safely.</summary>
[Serializable, NetSerializable]
public enum TacticalPlanIcon : byte
{
    None,
    Nuke,
    Disk,
    Captain,
    Clown,
    C4,
    Telecomms,
    Power,
    Biohazard,
}

/// <summary>
/// Pure input validation shared by the client and server. The server still decides whether
/// a valid drawing can be saved under the console's capacity and rate limits.
/// </summary>
public static class TacticalPlanDrawingValidation
{
    /// <summary>Maximum points allowed in one drawing message.</summary>
    public const int MaxPointsPerStroke = 256;

    /// <summary>Rejects malformed drawing input and normalizes valid ink to a visible color.</summary>
    public static bool TryValidate(TacticalPlanAnnotationKind kind, IReadOnlyList<Vector2>? points, Color chosen,
        out Color color, TacticalPlanIcon icon = TacticalPlanIcon.None)
    {
        color = default;
        if (!Enum.IsDefined(kind) || points == null
            || points.Count is < 1 or > MaxPointsPerStroke
            || (kind == TacticalPlanAnnotationKind.Dot && points.Count != 1)
            || (kind == TacticalPlanAnnotationKind.Stroke && points.Count < 2)
            || (kind == TacticalPlanAnnotationKind.Icon && (points.Count != 1
                || !Enum.IsDefined(icon) || icon == TacticalPlanIcon.None))
            || (kind != TacticalPlanAnnotationKind.Icon && icon != TacticalPlanIcon.None))
            return false;

        foreach (var point in points)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)
                || MathF.Abs(point.X) > 4096f || MathF.Abs(point.Y) > 4096f)
                return false;
        }

        if (!float.IsFinite(chosen.R) || !float.IsFinite(chosen.G) || !float.IsFinite(chosen.B))
            return false;

        var r = Math.Clamp(chosen.R, 0f, 1f);
        var g = Math.Clamp(chosen.G, 0f, 1f);
        var b = Math.Clamp(chosen.B, 0f, 1f);
        var brightest = MathF.Max(r, MathF.Max(g, b));
        if (brightest < 0.35f)
        {
            var scale = 0.35f / MathF.Max(brightest, 0.01f);
            r = Math.Clamp(r * scale, 0f, 1f);
            g = Math.Clamp(g * scale, 0f, 1f);
            b = Math.Clamp(b * scale, 0f, 1f);
            if (brightest == 0f)
                r = g = b = 0.35f;
        }

        color = new Color(r, g, b);
        return true;
    }
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
    List<int> removedIds, Color assignedColor, bool isFull, bool syndicatePalette, bool iconsUnlocked) : BoundUserInterfaceMessage
{
    public readonly string CacheToken = cacheToken;
    public readonly bool Full = full;
    public readonly List<TacticalPlanAnnotation> Annotations = annotations;
    public readonly List<int> RemovedIds = removedIds;
    public readonly Color AssignedColor = assignedColor;
    public readonly bool IsFull = isFull;
    public readonly bool SyndicatePalette = syndicatePalette;
    public readonly bool IconsUnlocked = iconsUnlocked;
}

[Serializable, NetSerializable]
public sealed class TacticalPlanAddAnnotationMessage(TacticalPlanAnnotationKind kind, List<Vector2> points, Color color,
    TacticalPlanIcon icon = TacticalPlanIcon.None) : BoundUserInterfaceMessage
{
    public readonly TacticalPlanAnnotationKind Kind = kind;
    public readonly List<Vector2> Points = points;
    public readonly Color Color = color;
    public readonly TacticalPlanIcon Icon = icon;
}

/// <summary>Reveals the icon tool to viewers already using a newly emagged Syndicate console.</summary>
[Serializable, NetSerializable]
public sealed class TacticalPlanIconsUnlockedMessage : BoundUserInterfaceMessage;

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
