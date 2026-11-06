using System.IO;
using Content.Shared._Starlight.Snapshots;
using Content.Shared.Photography;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Server._Starlight.Snapshots;

public sealed partial class SnapshotPersistenceSystem : EntitySystem
{
    [Dependency] private IRobustSerializer _serializer = default!;

    private const int MaxSerializedBytes = 8 * 1024 * 1024;
    private const int MaxEncodedLength = (MaxSerializedBytes + 2) / 3 * 4;

    public string? GetSerializedData(EntityUid uid)
        => !TryComp<SnapshotComponent>(uid, out var snapshot)
            ? null
            : snapshot.SerializedData ?? (snapshot.Data == null ? null : Serialize(snapshot.Data));

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
            var snapshot = EnsureComp<SnapshotComponent>(uid);
            snapshot.Data = data;
            snapshot.SerializedData = serialized;
            ApplyPhotographMetadata(uid, data);
            return true;
        }
        catch (Exception e)
        {
            // Dont let one bad photograph break them all
            Log.Error($"Failed to deserialize photograph data for {ToPrettyString(uid)}: {e.Message}");
            return false;
        }
    }

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
            return data;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to deserialize photograph data: {e.Message}");
            return null;
        }
    }

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
