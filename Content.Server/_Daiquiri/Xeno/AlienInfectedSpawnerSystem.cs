using System.Linq;
using Content.Server._Daiquiri.Xeno.Components;
using Content.Server.Antag;
using Content.Server.Preferences.Managers;
using Content.Shared.Antag;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Daiquiri.Xeno;

public sealed partial class AlienInfectedSpawnerSystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlienInfectedSpawnerComponent, AntagSelectEntityEvent>(OnSelectEntity);
    }

    private void OnSelectEntity(Entity<AlienInfectedSpawnerComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Handled)
            return;

        var infected = Spawn(ent.Comp.Prototype, args.Coords);
        args.Entity = infected;
        args.SelectedProfile = GetProfile(args.Antag, args.Session, infected);
    }

    private HumanoidCharacterProfile GetProfile(AntagSpecifierPrototype antag, ICommonSession? session, EntityUid infected)
    {
        if (session is { } player && _preferences.TryGetCachedPreferences(player.UserId, out var prefs))
        {
            var pool = prefs.Characters.Values
                .OfType<HumanoidCharacterProfile>()
                .Where(profile => profile.Enabled && _antagSelection.IsProfileValidForAntag(player, profile, antag))
                .ToList();

            if (pool.Count > 0)
                return _random.Pick(pool);
        }

        var species = SharedHumanoidAppearanceSystem.DefaultSpecies;
        if (TryComp<HumanoidAppearanceComponent>(infected, out var humanoid))
            species = humanoid.Species;

        return HumanoidCharacterProfile.RandomWithSpecies(species)
            .WithAntagPreferences(antag.PrefRoles);
    }
}
