using Content.Server._Starlight.Administration.Systems;
using Content.Server._Starlight.Traits;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid;
using Content.Server.Preferences.Managers;
using Content.Server.Traits;
using Content.Shared._Starlight.Character.Info;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules;

public sealed class AntagLoadProfileRuleSystem : GameRuleSystem<AntagLoadProfileRuleComponent>
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!; // Starlight
    [Dependency] private readonly TraitSystem _traitSystem = default!; //Starlight
    [Dependency] private readonly SLSharedCharacterInfoSystem _sLSharedCharacterInfoSystem = default!; //Starlight
    [Dependency] private readonly GrammarSystem _grammarSystem = default!; // Starlight
    [Dependency] private readonly AutoDiscordLogSystem _autolog = default!; // Starlight

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
        if (args.Session != null)
        {
            var roles = args.AntagRoles;
            var prefs = _prefs.GetPreferences(args.Session.UserId);
            profile = prefs.SelectProfileForAntag(roles);
        }

        // Startlight - Start (Changing fully so RandomWithSpecies loads with a specieID)
        var species = _proto.Index(SharedHumanoidAppearanceSystem.DefaultSpecies);
        if (profile is not null)
            species = _proto.Index(profile.Species);

        if (ent.Comp.SpeciesHardOverride is not null)
            species = _proto.Index(ent.Comp.SpeciesHardOverride.Value);
        else if (ent.Comp.SpeciesOverride is not null
            && (ent.Comp.SpeciesOverrideBlacklist?.Contains(new ProtoId<SpeciesPrototype>(species.ID)) ?? false))
            species = _proto.Index(ent.Comp.SpeciesOverride.Value);

        if (profile is null)
            profile = HumanoidCharacterProfile.RandomWithSpecies(species.ID);

        if (profile?.ForcedPrototype != "" && profile is not null)
        {
            if (!_proto.Resolve(profile.ForcedPrototype, out var forcedProto))
                throw new ArgumentException($"Could not find ${profile.ForcedPrototype} prototype for spawn rule.");
            args.Entity = Spawn(profile.ForcedPrototype);
            var resolvedEntity = (EntityUid)args.Entity;
            var grammar = EntityManager.EnsureComponent<GrammarComponent>(resolvedEntity);
            _grammarSystem.SetGender((resolvedEntity, grammar), profile.Gender);

            _autolog.LogToDiscord(Loc.GetString("autolog-forcedprototype", ("character", profile.Name), ("prototype", profile.ForcedPrototype)));
        }
        else
        {
            args.Entity = Spawn(species.Prototype);
            _humanoid.LoadProfile(args.Entity.Value, profile?.WithSpecies(species.ID));
        }

        if (ent.Comp.ApplyCharacterProfile && profile is not null)
        {
            _metaSystem.SetEntityName(args.Entity.Value, profile.Name);
            _sLSharedCharacterInfoSystem.ApplyCharacterInfo(args.Entity.Value, profile);
            if (args.Session is not null)
                _traitSystem.ApplyTraits(args.Entity.Value, profile, args.Session);
        }

        if (profile?.ForcedPrototype != "")
            RaiseLocalEvent(args.Entity.Value, new ForcedPrototypeDoSpecialEvent()); // Starlight
        // Starlight - End
    }
}
