using Content.Server._Starlight.Objectives.Events;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Server.Database.Migrations.Postgres;
using Content.Server.EUI;
using Content.Server.Ghost.Roles.UI;
using Content.Shared._Starlight.Railroading;
using Content.Shared._Starlight.Railroading.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Database;
using Content.Shared.Examine;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Starlight.Railroading;

public sealed partial class RailroadingSystem : SharedRailroadingSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly StarlightEntitySystem _entitySystem = default!;
    [Dependency] private readonly RailroadRuleSystem _railroadRule = default!;

    public readonly ProtoId<AlertPrototype> AlertProtoId = "RailroadingChoice";
    private readonly Dictionary<ICommonSession, CardSelectionEui> _openUis = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RailroadCardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RailroadableComponent, OpenCardsAlertEvent>(ShowCardsUi);
        SubscribeLocalEvent<RailroadableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RailroadableComponent, CollectObjectivesEvent>(OnCollectObjectiveInfo);
    }

    private void OnMapInit(Entity<RailroadCardComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Images != null && ent.Comp.Images.Count != 0)
            ent.Comp.Image = _random.Pick(ent.Comp.Images); // Randomly picks Image from collection.
    }

    private void OnCollectObjectiveInfo(Entity<RailroadableComponent> ent, ref CollectObjectivesEvent args)
    {
        var collect = new CollectObjectiveInfoEvent([]);

        if (ent.Comp.ActiveCard is { } card)
            RaiseLocalEvent(card, ref collect);

        if (ent.Comp.Completed is { Count: > 0 })
            foreach (var item in ent.Comp.Completed)
                RaiseLocalEvent(item, ref collect);


        args.Groups["Cards"] = collect.Objectives;
    }

    private void OnExamined(Entity<RailroadableComponent> ent, ref ExaminedEvent args)
    {
        // A lot of text, but luckily it’s only for admins.
        if (_admins.IsAdmin(args.Examiner))
        {
            using var group = args.PushGroup("Railroading");

            if (ent.Comp.ActiveCard is { } card)
            {
                (string, object)[] @params = [
                    ("Color", card.Comp1.Color),
                    ("IconColor", card.Comp1.IconColor),
                    ("Icon", card.Comp1.Icon),
                    ("Title", Loc.GetString(card.Comp1.Title)),
                    ("Desc", Loc.GetString(card.Comp1.Description)),
                ];
                args.PushMarkup(Loc.GetString("railroading-card-examined", @params));
            }
            if (ent.Comp.IssuedCards is { Count: > 0 } cards)
            {
                foreach (var item in cards)
                {
                    (string, object)[] @params = [
                        ("Color", item.Comp1.Color),
                        ("IconColor", item.Comp1.IconColor),
                        ("Icon", item.Comp1.Icon),
                        ("Title", Loc.GetString(item.Comp1.Title))
                    ];
                    args.PushMarkup(Loc.GetString("railroading-issued-card", @params));
                }
            }
        }
    }

    public Card EntToCard(Entity<RailroadCardComponent, RuleOwnerComponent> entity)
        => new()
        {
            Id = GetNetEntity(entity.Owner),
            Title = Loc.GetString(entity.Comp1.Title),
            Icon = entity.Comp1.Icon,
            Color = entity.Comp1.Color,
            IconColor = entity.Comp1.IconColor,
            Description = Loc.GetString(entity.Comp1.Description),
            Image = entity.Comp1.Image,

            CreditReward = !TryComp<RailroadDonationRewardComponent>(entity, out var creditReward) ? null : creditReward.Amount,
            HasSecretAccess = HasComp<RailroadSecretVendingAccessComponent>(entity)
        };

    private void ShowCardsUi(Entity<RailroadableComponent> ent, ref OpenCardsAlertEvent args)
    {
        if (!_players.TryGetSessionByEntity(ent.Owner, out var user) || _openUis.ContainsKey(user))
            return;

        var eui = _openUis[user] = new CardSelectionEui()
        {
            Subject = ent
        };
        _euiManager.OpenEui(eui, user);
        eui.StateDirty();
        if (TryComp<AlertsComponent>(ent, out var alerts))
            _alerts.ClearAlert((ent, alerts), AlertProtoId);
    }

    public void CloseEui(ICommonSession session)
    {
        if (!_openUis.ContainsKey(session))
            return;

        _openUis.Remove(session, out var eui);

        eui?.Close();
    }

    // todo: timer
    public void ShowAlert(EntityUid owner)
    {
        if (!_players.TryGetSessionByEntity(owner, out var user) || _openUis.ContainsKey(user))
            return;

        _alerts.ShowAlert(owner, AlertProtoId);
    }

    public void OnCardSelected(Entity<RailroadableComponent> subject, NetEntity cardNetUid)
    {
        if (_players.TryGetSessionByEntity(subject.Owner, out var user) && _openUis.ContainsKey(user))
            _openUis.Remove(user);

        var cardUid = GetEntity(cardNetUid);
        if (!cardUid.IsValid() || subject.Comp.IssuedCards is null)
            return;

        foreach (var card in subject.Comp.IssuedCards)
            if (card.Owner == cardUid)
            {
                var ev = new RailroadingAssignedEvent(subject);
                RaiseLocalEvent(card, ref ev);
                if (ev.Cancelled)
                {
                    Log.Warning($"Could not assign card {ToPrettyString(cardUid)}, deleted it");
                    if (_entitySystem.TryEntity<RailroadRuleComponent>(card.Comp2.RuleOwner, out var rule))
                        _railroadRule.AddCardToPool(rule, card);

                    continue;
                }

                subject.Comp.ActiveCard = card;
                card.Comp1.Subject = subject.Owner;
                _adminLogger.Add(LogType.Railroading, LogImpact.Medium, $"{ToPrettyString(subject)} selected card {ToPrettyString(cardUid)}.");

                var cardPerformer = EnsureComp<RailroadCardPerformerComponent>(card);
                cardPerformer.Performer = subject;

                var @event = new RailroadingCardChosenEvent(subject);
                RaiseLocalEvent(card, ref @event);
            }
            else if (_entitySystem.TryEntity<RailroadRuleComponent>(card.Comp2.RuleOwner, out var rule))
                _railroadRule.AddCardToPool(rule, card);

        subject.Comp.IssuedCards = null;
    }
    public void OnCardSelectionClosed(Entity<RailroadableComponent> subject)
    {
        if (_players.TryGetSessionByEntity(subject.Owner, out var user) && _openUis.ContainsKey(user))
            _openUis.Remove(user);

        if (subject.Comp.IssuedCards is null || subject.Comp.Important)
            return;

        foreach (var card in subject.Comp.IssuedCards)
            if (_entitySystem.TryEntity<RailroadRuleComponent>(card.Comp2.RuleOwner, out var rule))
                _railroadRule.AddCardToPool(rule, card);

        subject.Comp.IssuedCards = null;
        subject.Comp.Restricted = true;
    }

    internal void InvalidateProgress(Entity<RailroadableComponent> ent)
    {
        if (ent.Comp.ActiveCard is null)
            return;

        var @event = new RailroadingCardCompletionQueryEvent();
        RaiseLocalEvent(ent.Comp.ActiveCard.Value, ref @event);
        if (@event.IsCompleted != true)
            return;

        var completedEvent = new RailroadingCardCompletedEvent(ent);
        RaiseLocalEvent(ent.Comp.ActiveCard.Value, ref completedEvent);

        _adminLogger.Add(LogType.Railroading, LogImpact.Medium, $"{ToPrettyString(ent)} completed card {ToPrettyString(ent.Comp.ActiveCard.Value)}.");
        ent.Comp.Completed ??= [];
        ent.Comp.Completed.Add(ent.Comp.ActiveCard.Value);
        ent.Comp.ActiveCard = null;
    }

    internal void CardFailed(Entity<RailroadableComponent> ent)
    {
        if (ent.Comp.ActiveCard is null)
            return;

        var @event = new RailroadingCardFailedEvent(ent);
        RaiseLocalEvent(ent.Comp.ActiveCard.Value, ref @event);
        RaiseLocalEvent(ent, ref @event);

        _adminLogger.Add(LogType.Railroading, LogImpact.Medium, $"{ToPrettyString(ent)} failed card {ToPrettyString(ent.Comp.ActiveCard.Value)}.");
        ent.Comp.Completed ??= [];
        ent.Comp.Completed.Add(ent.Comp.ActiveCard.Value);
        ent.Comp.ActiveCard = null;
    }
}
