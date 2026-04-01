using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.CartridgeLoader;
using Content.Server._CD.CartridgeLoader.Cartridges;
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Systems;
using Robust.Server.Player;
using Content.Shared._CD.NanoChat;
using Content.Shared._Starlight.NanoChat;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking.Components;
using Content.Shared.PDA;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Localization;
using Content.Shared.Humanoid;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Content.Server.Station.Components;
using Content.Server.Mind;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Content.Shared._CD.CartridgeLoader.Cartridges;
using Content.Shared._Starlight.Time;
using Robust.Server.Containers;

namespace Content.Server._Starlight.GameTicking.Rules;

/// <summary>
/// Game rule that periodically sends spam advertisements via NanoChat.
/// </summary>
public sealed class NanoChatSpamRuleSystem : GameRuleSystem<NanoChatSpamRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedNanoChatSystem _nanoChat = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly TimeSystem _timeSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    
    private static readonly Regex RandomNumberPattern = new(@"\[\[randomnumber:(\d+):(\d+)\]\]", RegexOptions.Compiled);

    protected override void Started(EntityUid uid, NanoChatSpamRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        
        // Schedule first spam
        component.NextSpamTime = _random.NextFloat(component.MinDelay, component.MaxDelay);
    }

    protected override void ActiveTick(EntityUid uid, NanoChatSpamRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        component.NextSpamTime -= frameTime;

        if (component.NextSpamTime > 0)
            return;

        // Reset timer
        component.NextSpamTime += _random.NextFloat(component.MinDelay, component.MaxDelay);

        // Send spam
        SendSpamMessage(component);
    }

    private void SendSpamMessage(NanoChatSpamRuleComponent component)
    {
        // Get all advertisement prototypes
        var adPrototypes = _prototype.EnumeratePrototypes<NanoChatAdvertisementPrototype>().ToList();
        if (adPrototypes.Count == 0)
            return;

        // Get all PDA users with NanoChat cards
        var potentialRecipients = new List<Entity<NanoChatCardComponent>>();
        var cardQuery = EntityQueryEnumerator<NanoChatCardComponent, IdCardComponent>();
        
        while (cardQuery.MoveNext(out var cardUid, out var card, out _))
        {
            // Skip if no number assigned
            if (card.Number == null)
                continue;

            // Check if card is in a PDA that belongs to a player
            if (!TryFindPdaForCard(cardUid, out var pdaEntity))
                continue;

            if (pdaEntity == null)
                continue;

            // Check if PDA has NanoChat cartridge
            if (!HasNanoChatCartridge(pdaEntity.Value))
                continue;

            potentialRecipients.Add((cardUid, card));
        }

        if (potentialRecipients.Count == 0)
            return;

        // Select random recipients
        var numRecipients = Math.Min(
            component.MaxRecipientsPerMessage,
            (int)(potentialRecipients.Count * component.RecipientChance)
        );
        numRecipients = Math.Max(1, numRecipients); // At least 1 recipient
        numRecipients = Math.Min(numRecipients, potentialRecipients.Count); // Don't exceed available recipients

        // GetItems returns a span, convert to list and ensure uniqueness by UID
        var recipientsSpan = _random.GetItems(potentialRecipients, numRecipients);
        var uniqueRecipients = recipientsSpan.ToArray()
            .GroupBy(r => r.Owner)
            .Select(g => g.First())
            .ToList();

        // Send message to each unique recipient with a different rolled advertisement
        foreach (var recipient in uniqueRecipients)
        {
            var selectedAd = PickWeightedAdvertisement(adPrototypes);
            SendSpamToRecipient(recipient, selectedAd);
        }
    }

    private NanoChatAdvertisementPrototype PickWeightedAdvertisement(List<NanoChatAdvertisementPrototype> adPrototypes)
    {
        // Pick a random advertisement (weighted)
        var totalWeight = adPrototypes.Sum(ad => ad.Weight);
        var roll = _random.NextFloat() * totalWeight;
        var cumulative = 0f;

        foreach (var ad in adPrototypes)
        {
            cumulative += ad.Weight;
            if (roll <= cumulative)
            {
                return ad;
            }
        }

        // Fallback
        return _random.Pick(adPrototypes);
    }

    private void SendSpamToRecipient(Entity<NanoChatCardComponent> recipient, NanoChatAdvertisementPrototype ad)
    {
        if (recipient.Comp.Number == null)
            return;

        // Get recipient name
        var recipientName = "User";
        if (TryComp<IdCardComponent>(recipient, out var idCard))
        {
            recipientName = idCard.FullName ?? recipientName;
        }

        // Find the player entity that is holding/wearing the PDA
        EntityUid? playerEntity = null;
        if (TryFindPdaForCard(recipient, out var pdaEntity) && pdaEntity.HasValue)
        {
            // Get the entity containing the PDA (the player)
            if (_container.TryGetContainingContainer(pdaEntity.Value, out var pdaContainer))
            {
                playerEntity = pdaContainer.Owner;
            }
        }

        // Get job and department from the player entity
        var job = "Unknown";
        var department = "Unknown";
        if (playerEntity != null && _mind.TryGetMind(playerEntity.Value, out var mindId, out var mindComp))
        {
            // Get job from mind role
            if (_roles.MindHasRole<JobRoleComponent>(mindId, out var jobRole))
            {
                var jobProtoId = jobRole.Value.Comp1.JobPrototype;
                if (jobProtoId != null && _jobs.MindTryGetJob(mindId, out var jobProto))
                {
                    job = jobProto.LocalizedName;
                    
                    // Get department from job prototype using the proper system
                    if (_jobs.TryGetDepartment(jobProtoId, out var deptProto))
                    {
                        department = Loc.GetString(deptProto.Name);
                    }
                }
            }
        }

        // Get species from the player entity
        var species = "Human";
        if (playerEntity != null && TryComp<HumanoidAppearanceComponent>(playerEntity.Value, out var humanoid))
        {
            species = humanoid.Species.ToString();
        }

        // Get gender from the player entity
        var gender = "Unknown";
        if (playerEntity != null && TryComp<HumanoidAppearanceComponent>(playerEntity.Value, out var humanoidGender))
        {
            gender = humanoidGender.Gender.ToString();
        }

        // Get current station time and date using the proper TimeSystem
        var (timeSpan, dateString) = _timeSystem.GetStationTime();
        var stationTime = timeSpan.ToString(@"hh\:mm");
        var stationDate = dateString;

        // Get station name
        var stationName = "Unknown Station";
        var stationUid = _station.GetOwningStation(recipient);
        if (stationUid != null)
        {
            var meta = MetaData(stationUid.Value);
            stationName = meta.EntityName;
        }

        // Get NanoChat number
        var nanoChatNumber = recipient.Comp.Number?.ToString() ?? "0000";

        // Get random name from station manifest
        var randomName = GetRandomStationName(stationUid);

        // Get crew count
        var crewCount = _playerManager.PlayerCount.ToString();

        // First get the raw localized string
        var rawMessage = Loc.GetString(ad.MessageKey,
            ("recipient", recipientName),
            ("time", stationTime),
            ("station", stationName),
            ("sender", ad.SenderName),
            ("job", job),
            ("department", department),
            ("species", species),
            ("gender", gender),
            ("date", stationDate),
            ("nanochatnumber", nanoChatNumber),
            ("randomname", randomName),
            ("crewcount", crewCount)
        );

        // Process custom random number ranges
        var messageContent = ProcessRandomNumberPatterns(rawMessage);

        // Use fixed sender number from prototype, or generate from ID hash
        var senderNumber = ad.SenderNumber ?? GetSenderNumberForAd(ad.ID);

        // Create the spam message
        var message = new NanoChatMessage(
            _timing.CurTime,
            messageContent,
            senderNumber
        );

        // Add sender to recipients if not already present
        var recipients = _nanoChat.GetRecipients((recipient, recipient.Comp));
        if (!recipients.ContainsKey(senderNumber))
        {
            var senderRecipient = new NanoChatRecipient(
                senderNumber,
                ad.SenderName,
                "Advertisement"
            )
            {
                HasUnread = true
            };
            _nanoChat.SetRecipient((recipient, recipient.Comp), senderNumber, senderRecipient);
        }
        else
        {
            // Mark existing conversation as having unread
            var existing = recipients[senderNumber];
            _nanoChat.SetRecipient((recipient, recipient.Comp), senderNumber, existing with { HasUnread = true });
        }

        // Add message to recipient's inbox
        _nanoChat.AddMessage((recipient, recipient.Comp), senderNumber, message);

        // Trigger notification if not muted
        if (!_nanoChat.GetNotificationsMuted((recipient, recipient.Comp)))
        {
            var msgEv = new NanoChatMessageReceivedEvent(recipient, message);
            RaiseLocalEvent(ref msgEv);
        }
    }

    private bool TryFindPdaForCard(EntityUid cardUid, out EntityUid? pdaEntity)
    {
        pdaEntity = null;

        // Check if card is contained in something
        var transform = Transform(cardUid);
        var parent = transform.ParentUid;
        if (!parent.IsValid())
            return false;

        // Check if parent is a PDA
        if (HasComp<PdaComponent>(parent))
        {
            pdaEntity = parent;
            return true;
        }

        return false;
    }

    private bool HasNanoChatCartridge(EntityUid pdaEntity)
    {
        // Check if PDA has a cartridge loader with NanoChat cartridge
        if (!TryComp<CartridgeLoaderComponent>(pdaEntity, out var loader))
            return false;

        var installed = _cartridgeLoader.GetInstalled(pdaEntity);
        foreach (var cartridge in installed)
        {
            if (HasComp<NanoChatCartridgeComponent>(cartridge))
                return true;
        }

        return false;
    }
    
    /// <summary>
    /// Generate a consistent sender number for an advertisement ID.
    /// Uses hash to ensure same ad always gets same number.
    /// </summary>
    private uint GetSenderNumberForAd(string adId)
    {
        var hash = adId.GetHashCode();
        return 9000u + (uint)(Math.Abs(hash) % 1000);
    }

    /// <summary>
    /// Process custom random number patterns in the format [[randomnumber:min:max]].
    /// Replaces each occurrence with a random number in the specified range.
    /// Each pattern generates a new random number independently.
    /// </summary>
    private string ProcessRandomNumberPatterns(string message)
    {
        return RandomNumberPattern.Replace(message, match =>
        {
            if (int.TryParse(match.Groups[1].Value, out var min) && 
                int.TryParse(match.Groups[2].Value, out var max))
            {
                // Ensure min <= max
                if (min > max)
                    (min, max) = (max, min);
                
                // Generate a fresh random number for each match
                var randomValue = _random.Next(min, max + 1);
                return randomValue.ToString();
            }
            
            // If parsing fails, return the original match
            return match.Value;
        });
    }

    /// <summary>
    /// Get a random crew member name from the station manifest.
    /// </summary>
    private string GetRandomStationName(EntityUid? stationUid)
    {
        if (stationUid == null)
            return "John Doe";

        var allNames = new List<string>();
        
        // Collect all crew names from the station
        var query = EntityQueryEnumerator<IdCardComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var idCard, out var xform))
        {
            // Check if this card belongs to someone on this station
            var owningStation = _station.GetOwningStation(uid, xform);
            if (owningStation != stationUid)
                continue;

            if (!string.IsNullOrEmpty(idCard.FullName))
            {
                allNames.Add(idCard.FullName);
            }
        }

        return allNames.Count > 0 ? _random.Pick(allNames) : "John Doe";
    }
}
