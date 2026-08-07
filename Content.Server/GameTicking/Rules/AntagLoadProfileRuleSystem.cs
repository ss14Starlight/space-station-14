using System.Linq;
using Content.Server._Starlight.Administration.Systems;
using Content.Server._Starlight.Traits;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid;
using Content.Server.Preferences.Managers;
using Content.Shared._Starlight.Character.Info;
using Content.Shared._Starlight.Station;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

public sealed partial class AntagLoadProfileRuleSystem : GameRuleSystem<AntagLoadProfileRuleComponent>
{
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private AntagSelectionSystem _antagSelection = default!; // Starlight
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!; // Starlight
    [Dependency] private TraitSystem _traitSystem = default!; //Starlight
    [Dependency] private SLSharedCharacterInfoSystem _sLSharedCharacterInfoSystem = default!; //Starlight
    [Dependency] private GrammarSystem _grammarSystem = default!; // Starlight
    [Dependency] private AutoDiscordLogSystem _autolog = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagLoadProfileRuleComponent, AntagSelectEntityEvent>(OnSelectEntity);
    }

    private void OnSelectEntity(Entity<AntagLoadProfileRuleComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Handled)
            return;

        // Try to find a profile with this antagonist enabled on the player preferences
        HumanoidCharacterProfile? profile = null;
        if (args.Session is { } session) // Starlight
        {
            //var roles = args.AntagRoles; // Starlight
            var antag = args.Antag; // Starlight
            var prefs = _prefs.GetPreferences(args.Session.UserId);
            #region Starlight
            var profilePool = prefs.Characters.Values
            .OfType<HumanoidCharacterProfile>()
            .Where(candidate =>
            candidate.Enabled &&
            _antagSelection.IsProfileValidForAntag(
                session,
                candidate,
                antag))
            .ToList();

            if (profilePool.Count > 0)
                profile = RobustRandom.Pick(profilePool);
            #endregion
        }

        #region Starlight
        var species = Proto.Index(SharedHumanoidAppearanceSystem.DefaultSpecies);

        if (profile is not null)
            species = Proto.Index(profile.Species);

        if (ent.Comp.SpeciesHardOverride is not null)
            species = Proto.Index(ent.Comp.SpeciesHardOverride.Value);
        else if (ent.Comp.SpeciesOverride is not null
            && (ent.Comp.SpeciesOverrideBlacklist?.Contains(new ProtoId<SpeciesPrototype>(species.ID)) ?? false))
            species = Proto.Index(ent.Comp.SpeciesOverride.Value);

        profile ??= HumanoidCharacterProfile.RandomWithSpecies(species.ID);
        profile = profile.WithSpecies(species.ID);

        // This exact profile is subsequently used for its loadout.
        args.SelectedProfile = profile;

        if (!string.IsNullOrEmpty(profile.ForcedPrototype))
        {
            if (!Proto.Resolve(profile.ForcedPrototype, out var forcedProto))
                throw new ArgumentException($"Could not find ${profile.ForcedPrototype} prototype for spawn rule.");

            args.Entity = Spawn(profile.ForcedPrototype, args.Coords);
            var resolvedEntity = args.Entity.Value;
            var grammar = EntityManager.EnsureComponent<GrammarComponent>(resolvedEntity);
            _grammarSystem.SetGender((resolvedEntity, grammar), profile.Gender);

            _autolog.LogToDiscord(Loc.GetString("autolog-forcedprototype", ("character", profile.Name), ("prototype", profile.ForcedPrototype)));
        }
        else
        {
            args.Entity = Spawn(species.Prototype, args.Coords);
            _humanoid.LoadProfile(args.Entity.Value, profile);
        }

        if (ent.Comp.ApplyCharacterProfile)
        {
            _metaSystem.SetEntityName(args.Entity.Value, profile.Name);
            _sLSharedCharacterInfoSystem.ApplyCharacterInfo(args.Entity.Value, profile);

            if (args.Session is not null)
                _traitSystem.ApplyTraits(args.Entity.Value, profile, args.Session);
        }

        if (!string.IsNullOrEmpty(profile.ForcedPrototype))
            RaiseLocalEvent(args.Entity.Value, new ForcedPrototypeDoSpecialEvent());
    #endregion
    }
}
