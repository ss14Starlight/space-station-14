using Content.Shared.MassMedia.Systems;

// ReSharper disable once CheckNamespace once
namespace Content.Shared.CartridgeLoader.Cartridges;

public sealed partial class NewsReaderBoundUserInterfaceState
{
    public bool HasPhoto;

    public NewsReaderBoundUserInterfaceState(NewsArticle article, int targetNum, int totalNum, bool notificationOn, bool hasReacted, bool hasPhoto)
    {
        Article = article;
        TargetNum = targetNum;
        TotalNum = totalNum;
        NotificationOn = notificationOn;
        HasReacted = hasReacted;
        HasPhoto = hasPhoto;
    }
}
