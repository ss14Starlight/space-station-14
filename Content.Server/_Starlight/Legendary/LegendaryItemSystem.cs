using Content.Shared._Starlight.Legendary;
using Content.Server._NullLink.PlayerData;
using Content.Server.Preferences.Managers;
using Content.Shared._NullLink;
using Content.Shared.Humanoid;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using System.Linq;
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

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev) => ApplyPatronReferencesToPendingLegendaryItems();

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

        return true;
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

        // If we can find an eligible *Starlight* Patreon,
        // we inject their IC char name(off round) + pronouns into some story lines
        var patron = TryPickOnlinePatronOffRoundProfile(out var patronInfo) ? patronInfo : default(PatronStoryInfo?);
        var locArgs = patron?.ToLocArgs() ?? PatronStoryInfo.EmptyLocArgs;

        IReadOnlyList<LocId> opens = storyProto.Opens;
        IReadOnlyList<LocId> mids = storyProto.Mids;
        IReadOnlyList<LocId> ends = storyProto.Ends;

        // If we dont have a patron reference, do not select patron specific lines at all
        if (patron is null)
        {
            opens = opens.Where(k => !k.Id.Contains("-patron-", StringComparison.OrdinalIgnoreCase)).ToList();
            mids = mids.Where(k => !k.Id.Contains("-patron-", StringComparison.OrdinalIgnoreCase)).ToList();
            ends = ends.Where(k => !k.Id.Contains("-patron-", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (opens.Count == 0 || mids.Count == 0 || ends.Count == 0)
            return false;

        var open = Loc.GetString(_random.Pick(opens), locArgs);
        var mid = Loc.GetString(_random.Pick(mids), locArgs);
        var end = Loc.GetString(_random.Pick(ends), locArgs);
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
        // If we still camt find an eligible patron reference then we do nothing
        if (!TryPickOnlinePatronOffRoundProfile(out var _))
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

            // Rebuild story, at this point we expect "patron aware" keys to be eligible for selection
            if (!TryBuildStory(comp.Story.Value, comp.Description, out var story) || story == null)
                continue;

            var meta = MetaData(uid);
            _meta.SetEntityDescription(uid, story, meta);
            comp.PatronReferenceApplied = true;
        }
    }

    private bool TryPickOnlinePatronOffRoundProfile(out PatronStoryInfo info)
    {
        info = default;

        if (!_prototypeManager.TryIndex(_patronReq, out var patronReq) || patronReq.Roles.Length == 0)
            return false;

        var candidates = new List<HumanoidCharacterProfile>();

        foreach (var session in _playerManager.Sessions)
        {
            // We only consider online sessions that:
            // 1. have NullLink player data
            // 2. have the Discord role required by PatronReq
            // 3. have cached preferences
            if (!_nullLinkPlayerManager.TryGetPlayerData(session.UserId, out var playerData) 
            || !patronReq.Roles.Any(playerData.Roles.Contains) 
            || !_preferencesManager.TryGetCachedPreferences(session.UserId, out var prefs))
                continue;

            var enabledProfiles = prefs.Characters.Values
                .OfType<HumanoidCharacterProfile>()
                .Where(p => p.Enabled)
                .ToList();

            // If they only have one enabled character, do not reference them at all
            if (enabledProfiles.Count < 2)
                continue;

            HumanoidCharacterProfile? current = null;
            if (session.AttachedEntity is { Valid: true } ent
                && TryComp(ent, out HumanoidAppearanceComponent? humanoid)
                && humanoid.BaseProfile != null)
            {
                current = humanoid.BaseProfile;
            }

            // If the player is currently in-round, 
            // avoid referencing the character they playing rn
            // We compare to the BaseProfile snapshot stored on the humanoid
            var offRound = enabledProfiles
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

    private readonly record struct PatronStoryInfo(
        string Name,
        bool HasPatron,
        string Subject,
        string Object,
        string PossAdj,
        string PossPronoun,
        string Reflexive)
    {
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

        public static (string, object)[] MergeLocArgs((string, object)[] first, params (string, object)[] second)
        {
            if (second.Length == 0)
                return first;

            var merged = new (string, object)[first.Length + second.Length];
            Array.Copy(first, merged, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }

        private readonly record struct Pronouns(string Subject, string Object, string PossAdj, string PossPronoun, string Reflexive)
        {
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
