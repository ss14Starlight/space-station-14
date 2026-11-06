using Content.Shared._Moffstation.Snapshots;
using Robust.Shared.Serialization;

// ReSharper disable once CheckNamespace
namespace Content.Shared.MassMedia.Systems;

public abstract partial class SharedNewsSystem
{
    public const int MaxArticleSnapshotLength = 2 * 1024 * 1024; // Starlight: base64 chars, ~1.5MB raw snapshot data
    public const string PhotoSlotId = "photo";
}

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


