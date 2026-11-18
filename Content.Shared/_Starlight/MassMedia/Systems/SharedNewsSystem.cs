using System.Runtime.InteropServices;
using Content.Shared._Starlight.Snapshots;
using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace once
namespace Content.Shared.MassMedia.Systems;

public abstract partial class SharedNewsSystem
{
    public const int MaxArticleSnapshotLength = 2 * 1024 * 1024; // base64 chars, ~1.5MB snapshot data
    public const string PhotoSlotId = "photo";
}

[StructLayout(LayoutKind.Auto)]
public partial struct NewsArticle
{
    [ViewVariables]
    public string? ArticleSnapshotData;
}

[Serializable, NetSerializable]
public sealed class NewsArticlePhotoDataEvent(SnapshotData data) : EntityEventArgs
{
    public readonly SnapshotData Data = data;
}
