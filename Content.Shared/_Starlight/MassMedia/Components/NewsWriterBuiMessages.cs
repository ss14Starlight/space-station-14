using Content.Shared.MassMedia.Systems;

// ReSharper disable once CheckNamespace once
namespace Content.Shared.MassMedia.Components;

public sealed partial class NewsWriterBoundUserInterfaceState
{
    public readonly bool HasPhoto;

    public NewsWriterBoundUserInterfaceState(NewsArticle[] articles, bool publishEnabled, TimeSpan nextPublish, string draftTitle, string draftContent, bool hasPhoto)
    {
        Articles = articles;
        PublishEnabled = publishEnabled;
        NextPublish = nextPublish;
        DraftTitle = draftTitle;
        DraftContent = draftContent;
        HasPhoto = hasPhoto;
    }
}
