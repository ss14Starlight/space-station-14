using Content.Shared._Starlight.Legendary;
using Content.Server._NullLink.PlayerData;
using Content.Server.Preferences.Managers;
using Content.Shared._NullLink;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Preferences;
using System.Linq;
using Content.Server._Starlight.Legendary.Visuals;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Enums;

namespace Content.Server._Starlight.Legendary;

public sealed class LegendaryItemSystem : EntitySystem
{
    private static readonly ProtoId<RoleRequirementPrototype> _patronReq = "PatronReq";
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly INullLinkPlayerManager _nullLinkPlayerManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;

    /// <summary>
    /// Cached story reference info built from an online Starlight Patreon subscriber's off-round character.
    /// </summary>
    private PatronStoryInfo? _cachedStoryReference;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendaryItemComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnMapInit(EntityUid uid, LegendaryItemComponent component, ref MapInitEvent args)
    {
        if (component.RollProcessed)
            return;

        component.RollProcessed = true;

        if (!TryApplyLegendary(uid, component))
        {
            RemCompDeferred<LegendaryItemComponent>(uid);
            return;
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // Try to find an online Starlight Patreon subscriber and use their off-round character
        if (_cachedStoryReference == null && TryPickOnlinePatronOffRoundProfile(out var info))
            _cachedStoryReference = info;

        ApplyPatronReferencesToPendingLegendaryItems();
    }

    internal bool TryApplyLegendary(EntityUid uid, LegendaryItemComponent component)
    {
        var chance = Math.Clamp(component.Chance, 0f, 1f);
        if (chance <= 0f || !_random.Prob(chance))
            return false;

        component.LegendaryApplied = true;

        var description = GetDescription(component);
        if (description != null)
        {
            var meta = MetaData(uid);
            _meta.SetEntityDescription(uid, description, meta);
        }

        ApplyLegendarySprite(uid, component);

        EnsureComp<LegendaryAuraComponent>(uid); // Aura farming with this one

        return true;
    }

    private void ApplyLegendarySprite(EntityUid uid, LegendaryItemComponent component)
    {
        if (component.LegendarySprites.Count == 0)
            return;

        var rsiPath = _random.Pick(component.LegendarySprites);
        var fullPath = "/Textures/" + rsiPath.ToString();

        var spriteComp = EnsureComp<LegendarySpriteComponent>(uid);
        spriteComp.RsiPath = rsiPath;
        _item.SetSprite(uid, fullPath);
        _clothing.SetSprite(uid, fullPath);

        Dirty(uid, spriteComp);
    }

    private string? GetDescription(LegendaryItemComponent component)
    {
        if (component.Story is { } storyId && TryBuildStory(storyId, component.Description, out var story))
            return story;

        if (component.Description != null)
            return Loc.GetString(component.Description.Value);

        return null;
    }

    private bool TryBuildStory(ProtoId<StoryPrototype> storyId, LocId? template, out string? result)
    {
        result = null;

        if (!_prototypeManager.TryIndex(storyId, out StoryPrototype? storyProto))
            return false;

        if (storyProto.Opens.Count == 0 || storyProto.Mids.Count == 0 || storyProto.Ends.Count == 0)
            return false;

        var patron = _cachedStoryReference;
        var locArgs = patron?.ToLocArgs() ?? PatronStoryInfo.EmptyLocArgs;

        IReadOnlyList<LocId> opens = storyProto.Opens;
        IReadOnlyList<LocId> mids = storyProto.Mids;
        IReadOnlyList<LocId> ends = storyProto.Ends;

        static bool IsPatronKey(LocId key)
            => key.Id.Contains("-patron-", StringComparison.OrdinalIgnoreCase);

        // If we dont have a patron reference, do not select patron specific lines at all
        if (patron is null)
        {
            opens = opens.Where(k => !IsPatronKey(k)).ToList();
            mids = mids.Where(k => !IsPatronKey(k)).ToList();
            ends = ends.Where(k => !IsPatronKey(k)).ToList();
        }

        if (opens.Count == 0 || mids.Count == 0 || ends.Count == 0)
            return false;

        // Patron reference keys may include $patronName. We keep it at most once per generated description

        var openKey = _random.Pick(opens);

        IReadOnlyList<LocId> midPool = mids;
        IReadOnlyList<LocId> endPool = ends;

        if (patron != null)
        {
            var patronOpens = opens.Where(IsPatronKey).ToList();
            var patronMids = mids.Where(IsPatronKey).ToList();
            var patronEnds = ends.Where(IsPatronKey).ToList();

            // Use only patron lines when available
            if (patronOpens.Count > 0)
                opens = patronOpens;
            if (patronMids.Count > 0)
                midPool = patronMids;
            if (patronEnds.Count > 0)
                endPool = patronEnds;
        }

        var midKey = _random.Pick(midPool);
        var endKey = _random.Pick(endPool);

        var open = Loc.GetString(openKey, locArgs);
        var mid = Loc.GetString(midKey, locArgs);
        var end = Loc.GetString(endKey, locArgs);
        var combined = $"{open} {mid} {end}";

        if (template != null)
        {
            var args = PatronStoryInfo.MergeLocArgs(
                patron?.ToLocArgs() ?? PatronStoryInfo.EmptyLocArgs,
                ("open", open),
                ("mid", mid),
                ("end", end),
                ("story", combined));

            result = Loc.GetString(template.Value, args);
            return true;
        }

        result = combined;
        return true;
    }

    private void ApplyPatronReferencesToPendingLegendaryItems()
    {
        // This is a retry pass for items that already became legendary
        if (_cachedStoryReference == null)
            return;

        var query = EntityQueryEnumerator<LegendaryItemComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.RollProcessed || !comp.LegendaryApplied)
                continue;

            if (comp.PatronReferenceApplied)
                continue;

            if (comp.Story is null)
                continue;

            // Rebuild story, since now we have the reference available
            if (!TryBuildStory(comp.Story.Value, comp.Description, out var story) || story == null)
                continue;

            var meta = MetaData(uid);
            _meta.SetEntityDescription(uid, story, meta);
            comp.PatronReferenceApplied = true;
        }
    }

    /// <summary>
    /// Tries to find an online player with Starlight Patreon subscription and pick one of their off-round characters.
    /// </summary>
    private bool TryPickOnlinePatronOffRoundProfile(out PatronStoryInfo info)
    {
        info = default;

        if (!_prototypeManager.TryIndex(_patronReq, out var patronReq) || patronReq.Roles.Length == 0)
            return false;

        var candidates = new List<HumanoidCharacterProfile>();

        foreach (var session in _playerManager.Sessions)
        {
            // Check if this player has Starlight Patreon subscription
            if (!_nullLinkPlayerManager.TryGetPlayerData(session.UserId, out var playerData)
                || !patronReq.Roles.Any(playerData.Roles.Contains))
                continue;

            if (!_preferencesManager.TryGetCachedPreferences(session.UserId, out var prefs))
                continue;

            // Get all profiles
            var allProfiles = prefs.Characters.Values
                .OfType<HumanoidCharacterProfile>()
                .ToList();

            if (allProfiles.Count == 0)
                continue;

            // Find the character theyre currently playing
            HumanoidCharacterProfile? current = null;
            if (session.AttachedEntity is { Valid: true } ent
                && TryComp(ent, out HumanoidAppearanceComponent? humanoid)
                && humanoid.BaseProfile != null)
            {
                current = humanoid.BaseProfile;
            }

            // Get off-round profiles
            var offRound = allProfiles
                .Where(p => current == null || !p.MemberwiseEquals(current))
                .ToList();

            if (offRound.Count == 0)
                continue;

            candidates.AddRange(offRound);
        }

        if (candidates.Count == 0)
            return false;

        var chosen = _random.Pick(candidates);
        info = PatronStoryInfo.FromProfile(chosen);
        return true;
    }

    /// <summary>
    /// An a container for the localization arguments used by
    /// legendary story lines that reference a patrons
    /// </summary>
    private readonly record struct PatronStoryInfo(
        string Name,
        bool HasPatron,
        string Subject,
        string Object,
        string PossAdj,
        string PossPronoun,
        string Reflexive)
    {
        /// <summary>
        /// Localization args for the "no patron" case
        /// </summary>
        public static readonly (string, object)[] EmptyLocArgs =
        [
            ("hasPatron", false),
            ("patronName", string.Empty),
            ("patronSubject", string.Empty),
            ("patronObject", string.Empty),
            ("patronPossAdj", string.Empty),
            ("patronPossPronoun", string.Empty),
            ("patronReflexive", string.Empty),
        ];

        /// <summary>
        /// Creates a <see cref="PatronStoryInfo"/> from a humanoid character profile
        /// </summary>
        public static PatronStoryInfo FromProfile(HumanoidCharacterProfile profile)
        {
            var pronouns = Pronouns.FromGender(profile.Gender);
            return new PatronStoryInfo(
                profile.Name,
                true,
                pronouns.Subject,
                pronouns.Object,
                pronouns.PossAdj,
                pronouns.PossPronoun,
                pronouns.Reflexive);
        }

        /// <summary>
        /// Builds a loc.. argument array for inserting the patron name + pronouns
        /// </summary>
        public (string, object)[] ToLocArgs()
            =>
            [
                ("hasPatron", HasPatron),
                ("patronName", Name),
                ("patronSubject", Subject),
                ("patronObject", Object),
                ("patronPossAdj", PossAdj),
                ("patronPossPronoun", PossPronoun),
                ("patronReflexive", Reflexive),
            ];

        /// <summary>
        /// Merges two loc. argument lists into a single array
        /// </summary>
        public static (string, object)[] MergeLocArgs((string, object)[] first, params (string, object)[] second)
        {
            if (second.Length == 0)
                return first;

            var merged = new (string, object)[first.Length + second.Length];
            Array.Copy(first, merged, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }

        /// <summary>
        /// Pronouns set used for legendary story lines
        /// </summary>
        private readonly record struct Pronouns(string Subject, string Object, string PossAdj, string PossPronoun, string Reflexive)
        {
            // Maps a character profile gender to a set of pronouns
            public static Pronouns FromGender(Gender gender)
                => gender switch
                {
                    Gender.Male => new Pronouns("he", "him", "his", "his", "himself"),
                    Gender.Female => new Pronouns("she", "her", "her", "hers", "herself"),
                    Gender.Epicene => new Pronouns("they", "them", "their", "theirs", "themselves"),
                    _ => new Pronouns("it", "it", "its", "its", "itself"),
                };
        }
    }
}
