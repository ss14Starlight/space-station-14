using Content.Shared._Starlight.Legendary;
using Content.Server._NullLink.PlayerData;
using Content.Server.Preferences.Managers;
using Content.Shared._NullLink;
using Content.Shared.Humanoid;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using System.Linq;
using Content.Server.Cargo.Components;
using Content.Shared.Prayer;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server._Starlight.Legendary.Modifiers;
using Content.Server._Starlight.Legendary.Visuals;
using Content.Server.Weapons.Ranged.Systems;
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
    [Dependency] private readonly GunSystem _gunSystem = default!;

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

        ApplyLegendaryStatBonuses(uid, component);

        EnsureComp<LegendaryAuraComponent>(uid); // Aura farming with this one

        return true;
    }

    private void ApplyLegendaryStatBonuses(EntityUid uid, LegendaryItemComponent component)
    {
        if (component.Story is not { } storyId)
            return;

        switch (storyId.Id)
        {
            case "Firearm":
                ApplyFirearmBonuses(uid);
                break;
            case "MeleeWeapon":
                ApplyMeleeBonuses(uid);
                break;
            case "Clothing":
                ApplyClothingBonuses(uid);
                break;
            case "Trinket":
                EnsureComp<PrayableComponent>(uid);
                break;
            case "Plush":
                ApplyPlushBonuses(uid);
                break;
        }
    }

    private void ApplyFirearmBonuses(EntityUid uid)
    {
        // Unfortunatly i cant directly write to GunComponent here access is restricted to SharedGunSystem nor GunSystem
        // Instead attach "bonus" component and let it modify GunRefreshModifiersEvent
        var bonus = EnsureComp<LegendaryGunFireRateBonusComponent>(uid);
        bonus.FireRateBonus = 1f;

        if (TryComp(uid, out GunComponent? gun))
            _gunSystem.RefreshModifiers((uid, gun));
    }

    private void ApplyMeleeBonuses(EntityUid uid)
    {
        if (!TryComp(uid, out MeleeWeaponComponent? melee))
            return;

        melee.AttackRate = Math.Max(0.1f, melee.AttackRate + 1f);
        DirtyField(uid, melee, nameof(MeleeWeaponComponent.AttackRate));
    }

    // Same deal as with firearm 
    private void ApplyClothingBonuses(EntityUid uid) => EnsureComp<LegendaryArmorBonusComponent>(uid);

    private void ApplyPlushBonuses(EntityUid uid)
    {
        var staticPrice = EnsureComp<StaticPriceComponent>(uid);
        staticPrice.Price = 10000;
        Dirty(uid, staticPrice);
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
            var allowPatronInMid = _random.Prob(0.5f);

            if (allowPatronInMid)
            {
                var nonPatronEnds = ends.Where(k => !IsPatronKey(k)).ToList();
                if (nonPatronEnds.Count > 0)
                    endPool = nonPatronEnds;
            }
            else
            {
                var nonPatronMids = mids.Where(k => !IsPatronKey(k)).ToList();
                if (nonPatronMids.Count > 0)
                    midPool = nonPatronMids;
            }
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
            // We compare to the BaseProfile snapshot stored on thumanoid
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
