using System.IO;
using Content.Shared._Starlight.Snapshots;
using Content.Shared.Photography;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Server._Starlight.Snapshots;

/// <summary>
/// The scene a snapshot shows. Saved server-side and sent on request.
/// </summary>
[RegisterComponent]
public sealed partial class SnapshotComponent : Component
{
    /// <summary>
    /// Binary-serialized snapshot content, stored base64 so map serialization preserves photographs.
    /// </summary>
    [DataField]
    public string? SerializedData;

    /// <summary>
    /// Deserialized snapshot data, when available. See <see cref="SnapshotPersistenceSystem"/> for how the two are kept in sync.
    /// </summary>
    [ViewVariables]
    public SnapshotData? Data;
}

/// <summary>
/// Serializes <see cref="SnapshotData"/> to base64 for map persistence and restores it on map init.
/// Enforces the 8 MiB (de)serialization limit in one place.
/// </summary>
public sealed partial class SnapshotPersistenceSystem : EntitySystem
{
    [Dependency] private IRobustSerializer _serializer = default!;

    private const int MaxSerializedBytes = 8 * 1024 * 1024;
    private const int MaxEncodedLength = (MaxSerializedBytes + 2) / 3 * 4;
    private const int MaxDeserializedEntities = 512;
    private const int MaxDeserializedTiles = 4096;
    private const int MaxDeserializedOccluders = 1024;

    /// <summary>
    /// Returns the base64 serialized snapshot data for the entity, or null if none is available.
    /// Returns the stored <c>SerializedData</c> when present; otherwise serializes <c>Data</c> on demand, which may return null if serialization exceeds the 8 MiB limit.
    /// </summary>
    public string? GetSerializedData(EntityUid uid)
        => !TryComp<SnapshotComponent>(uid, out var snapshot)
            ? null
            : snapshot.SerializedData ?? (snapshot.Data == null ? null : Serialize(snapshot.Data));

    /// <summary>
    /// Stores the snapshot data and its serialized form on the entity, updating <see cref="PhotographComponent"/> metadata.
    /// Returns false without modifying the entity when serialization exceeds the 8 MiB limit; otherwise returns true.
    /// </summary>
    public bool SetData(EntityUid uid, SnapshotData data)
    {
        var serialized = Serialize(data);
        if (serialized == null)
            return false;

        var snapshot = EnsureComp<SnapshotComponent>(uid);
        snapshot.Data = data;
        snapshot.SerializedData = serialized;
        ApplyPhotographMetadata(uid, data);
        return true;
    }

    /// <summary>
    /// Validates and stores base64 serialized snapshot data, setting <c>Data</c> and <c>SerializedData</c> and updating <see cref="PhotographComponent"/> metadata.
    /// Returns false and leaves the entity unchanged when the input exceeds size limits or is malformed and fails to deserialize; otherwise returns true.
    /// </summary>
    public bool SetSerializedData(EntityUid uid, string serialized)
    {
        if (serialized.Length > MaxEncodedLength)
        {
            Log.Warning($"Rejected oversized serialized photograph data for {ToPrettyString(uid)}.");
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(serialized);
            if (bytes.Length > MaxSerializedBytes)
            {
                Log.Warning($"Rejected oversized serialized photograph data for {ToPrettyString(uid)}.");
                return false;
            }

            using var stream = new MemoryStream(bytes, writable: false);
            _serializer.DeserializeDirect(stream, out SnapshotData data);
            if (!IsWithinDeserializedLimits(data))
            {
                Log.Warning($"Rejected photograph data with oversized collections for {ToPrettyString(uid)}.");
                return false;
            }

            var snapshot = EnsureComp<SnapshotComponent>(uid);
            snapshot.Data = data;
            snapshot.SerializedData = serialized;
            ApplyPhotographMetadata(uid, data);
            return true;
        }
        catch (Exception e)
        {
            // Don't let one bad photograph break them all.
            Log.Error($"Failed to deserialize photograph data for {ToPrettyString(uid)}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deserializes base64 snapshot data without modifying anything. Returns null when the input exceeds size
    /// limits or is malformed. Use <see cref="SetSerializedData"/> to store it.
    /// </summary>
    public SnapshotData? TryDeserialize(string serialized)
    {
        if (serialized.Length > MaxEncodedLength)
            return null;

        try
        {
            var bytes = Convert.FromBase64String(serialized);
            if (bytes.Length > MaxSerializedBytes)
                return null;

            using var stream = new MemoryStream(bytes, writable: false);
            _serializer.DeserializeDirect(stream, out SnapshotData data);
            return IsWithinDeserializedLimits(data) ? data : null;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to deserialize photograph data: {e.Message}");
            return null;
        }
    }

    private static bool IsWithinDeserializedLimits(SnapshotData data)
        => data.Entities.Count <= MaxDeserializedEntities
            && data.Tiles.Count <= MaxDeserializedTiles
            && data.Hidden.Count <= MaxDeserializedTiles
            && data.Occluders.Count <= MaxDeserializedOccluders;

    /// <summary>
    /// Serializes snapshot data to base64. Returns null and logs a warning when over the size limit.
    /// </summary>
    private string? Serialize(SnapshotData data)
    {
        using var stream = new MemoryStream();
        _serializer.SerializeDirect(stream, data);
        if (stream.Length > MaxSerializedBytes)
        {
            Log.Warning($"Photograph data exceeds the {MaxSerializedBytes}-byte persistence limit.");
            return null;
        }

        // GetBuffer avoids the ToArray() copy, stream was created writable with a public buffer.
        return stream.TryGetBuffer(out var buffer) ? Convert.ToBase64String(buffer.Array!, buffer.Offset, (int) stream.Length) : Convert.ToBase64String(stream.ToArray());
    }

    /// <summary>
    /// Syncs subject examine text from the snapshot into the photograph component.
    /// </summary>
    private void ApplyPhotographMetadata(EntityUid uid, SnapshotData data)
    {
        var photograph = EnsureComp<PhotographComponent>(uid);
        photograph.NameText = data.SubjectNameText;
        if (data.SubjectDescription is { } description)
        {
            var message = new FormattedMessage();
            message.AddMarkupPermissive(description);
            photograph.Description = message;
        }
        else
        {
            photograph.Description = null;
        }

        Dirty(uid, photograph);
    }

    /// <summary>
    /// Restores snapshot data from map-serialized text on load. Clears the snapshot when the data no longer validates.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<SnapshotComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SerializedData is not { } serialized)
            return;

        if (!SetSerializedData(ent.Owner, serialized))
        {
            ent.Comp.Data = null;
            ent.Comp.SerializedData = null;
        }
    }
}
