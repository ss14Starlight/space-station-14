using Robust.Shared.Serialization;

namespace Content.Shared.MassMedia.Systems;

public abstract class SharedNewsSystem : EntitySystem
{
    public const int MaxTitleLength = 35; // Starlight, slight increase
    public const int MaxContentLength = 3072; // Starlight, +50% increase
}

[Serializable, NetSerializable]
public struct NewsArticle
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string Title;

    [ViewVariables(VVAccess.ReadWrite)]
    public string Content;

    [ViewVariables(VVAccess.ReadWrite)]
    public string? Author;

    [ViewVariables]
    public ICollection<(NetEntity, uint)>? AuthorStationRecordKeyIds;

    [ViewVariables]
    public TimeSpan ShareTime;
    // Starlight-edit: start
    [ViewVariables(VVAccess.ReadWrite)]
    public int Likes;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Dislikes;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Views;
    // Starlight-edit: end
}

[ByRefEvent]
public record struct NewsArticlePublishedEvent(NewsArticle Article);

[ByRefEvent]
public record struct NewsArticleDeletedEvent;
