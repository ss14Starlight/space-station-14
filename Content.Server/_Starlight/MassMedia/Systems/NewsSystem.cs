using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.MassMedia.Components;
using Content.Server._Starlight.Snapshots;
using Content.Shared._Starlight.CCVar;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.MassMedia.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Players.RateLimiting;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Server.MassMedia.Systems;

public sealed partial class NewsSystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SnapshotPersistenceSystem _snapshotPersistence = default!;
    [Dependency] private SharedPlayerRateLimitManager _rateLimit = default!;
    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private const string PhotoRateLimitKey = "NewsPhotoView";

    private void InitializeNewsPhoto()
        => _rateLimit.Register(PhotoRateLimitKey,
            new RateLimitRegistration(StarlightCCVars.SnapshotViewRateLimitPeriod,
                StarlightCCVars.SnapshotViewRateLimitCount,
                null));

    [SubscribeLocalEvent]
    private void OnNewsPhotoSlotChanged(Entity<NewsWriterComponent> ent, ref EntInsertedIntoContainerMessage args)
        => OnNewsPhotoSlotChanged(ent, args.Container.ID);

    [SubscribeLocalEvent]
    private void OnNewsPhotoSlotChanged(Entity<NewsWriterComponent> ent, ref EntRemovedFromContainerMessage args)
        => OnNewsPhotoSlotChanged(ent, args.Container.ID);

    private void OnNewsPhotoSlotChanged(Entity<NewsWriterComponent> ent, string containerId)
    {
        if (containerId != PhotoSlotId)
            return;

        UpdateWriterUi(ent);
    }

    private (string? SnapshotData, bool Oversized) GetAttachedPhotoData(Entity<NewsWriterComponent> ent)
    {
        if (_itemSlots.GetItemOrNull(ent.Owner, PhotoSlotId) is not { } photo)
            return (null, false);

        var data = _snapshotPersistence.GetSerializedData(photo);
        if (data is not null && data.Length > MaxArticleSnapshotLength)
        {
            _popup.PopupEntity(Loc.GetString("news-write-photo-too-large-popup"), ent, PopupType.SmallCaution);
            return (data, true);
        }

        return (data, false);
    }

    private void EjectAttachedPhoto(Entity<NewsWriterComponent> ent, EntityUid actor)
    {
        if (!_itemSlots.TryEject(ent.Owner, PhotoSlotId, actor, out var item))
            return;

        _hands.PickupOrDrop(actor, item.Value);
    }

    private void TrySendNewsArticlePhoto(Entity<NewsReaderCartridgeComponent> ent, EntityUid actor)
    {
        if (!TryGetArticles(ent, out var articles) || articles.Count == 0)
            return;

        NewsReaderLeafArticle(ent, 0);
        var serialized = articles[ent.Comp.ArticleNumber].ArticleSnapshotData;
        if (string.IsNullOrEmpty(serialized))
            return;

        if (!_actorQuery.TryComp(actor, out var actorComp))
            return;

        if (_rateLimit.CountAction(actorComp.PlayerSession, PhotoRateLimitKey) != RateLimitStatus.Allowed)
            return;

        var data = _snapshotPersistence.TryDeserialize(serialized);
        if (data is null)
            return;

        RaiseNetworkEvent(new NewsArticlePhotoDataEvent(data), actorComp.PlayerSession);
    }

    private string GetDiscordDescription(NewsArticle article)
    {
        var description = FormattedMessage.RemoveMarkupPermissive(article.Content);
        if (!string.IsNullOrEmpty(article.ArticleSnapshotData))
            description += $"\n\n{Loc.GetString("news-discord-photo-note")}";
        return description;
    }
}
