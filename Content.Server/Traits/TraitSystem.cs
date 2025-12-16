using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Content.Server._Starlight.Language; // Starlight
using Content.Shared.Tag;
using System.Linq;
using Content.Shared.Preferences; // Starlight
using Content.Shared.Body.Components; // Starlight - breathing traits
using Content.Shared.Body.Systems; // Starlight - breathing traits
using Content.Server.Body.Components; // Starlight - breathing traits
using Content.Shared.Body.Organ; // Starlight - breathing traits

namespace Content.Server.Traits;

public sealed class TraitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!; // Starlight - breathing traits
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // When the player is spawned in, add all trait components selected during character creation
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !_prototypeManager.Resolve<JobPrototype>(args.JobId, out var protoJob) ||
            !protoJob.ApplyTraits)
        {
            return;
        }

        #region Starlight Traits on spawn here
        ApplyTraits(args.Mob, args.Profile);
    }

    public void ApplyTraits(EntityUid Mob, HumanoidCharacterProfile Profile)
    {
        // Starlight - start: Track breathing traits to prevent conflicts
        var hasNitrogenTrait = Profile.TraitPreferences.Contains("NitrogenBreather");
        var hasOxygenTrait = Profile.TraitPreferences.Contains("OxygenBreather");

        // If both breathing traits are selected, skip both (mutual exclusivity)
        if (hasNitrogenTrait && hasOxygenTrait)
        {
            hasNitrogenTrait = false;
            hasOxygenTrait = false;
            Log.Warning($"Player attempted to select both NitrogenBreather and OxygenBreather traits - both will be ignored");
        }

        // Check existing lung type to prevent redundant traits
        var existingLungAlert = GetLungAlert(Mob);
        if (existingLungAlert != null)
        {
            if (hasNitrogenTrait && existingLungAlert == "LowNitrogen")
            {
                hasNitrogenTrait = false;
                Log.Info($"Player already breathes nitrogen, skipping NitrogenBreather trait");
            }
            else if (hasOxygenTrait && existingLungAlert == "LowOxygen")
            {
                hasOxygenTrait = false;
                Log.Info($"Player already breathes oxygen, skipping OxygenBreather trait");
            }
        }
        // Starlight - end

        foreach (var traitId in Profile.TraitPreferences)
        #endregion Starlight Traits on spawn here
        {
            // Starlight - start: Skip breathing traits if they were filtered out
            if ((traitId == "NitrogenBreather" && !hasNitrogenTrait) ||
                (traitId == "OxygenBreather" && !hasOxygenTrait))
            {
                continue;
            }
            // Starlight - end

            if (!_prototypeManager.TryIndex<TraitPrototype>(traitId, out var traitPrototype))
            {
                Log.Error($"No trait found with ID {traitId}!");
                return;
            }

            if (_whitelistSystem.IsWhitelistFail(traitPrototype.Whitelist, Mob) ||
                _whitelistSystem.IsWhitelistPass(traitPrototype.Blacklist, Mob))
                continue;

            // Add all components required by the prototype
            if (traitPrototype.Components.Count > 0)
                EntityManager.AddComponents(Mob, traitPrototype.Components, false);

            // Add all JobSpecials required by the prototype
            foreach (var special in traitPrototype.Specials)
            {
                special.AfterEquip(Mob);
            }

			// Starlight - start
            var language = EntityManager.System<LanguageSystem>();

            if (traitPrototype.RemoveLanguagesSpoken is not null)
                foreach (var lang in traitPrototype.RemoveLanguagesSpoken)
                    language.RemoveLanguage(Mob, lang, true, false);

            if (traitPrototype.RemoveLanguagesUnderstood is not null)
                foreach (var lang in traitPrototype.RemoveLanguagesUnderstood)
                    language.RemoveLanguage(Mob, lang, false, true);

            if (traitPrototype.LanguagesSpoken is not null)
                foreach (var lang in traitPrototype.LanguagesSpoken)
                    language.AddLanguage(Mob, lang, true, false);

            if (traitPrototype.LanguagesUnderstood is not null)
                foreach (var lang in traitPrototype.LanguagesUnderstood)
                    language.AddLanguage(Mob, lang, false, true);

            if (!string.IsNullOrEmpty(traitPrototype.Background))
            {
                var tag = new ProtoId<TagPrototype>(traitPrototype.Background + "TraitBackground");
                _tag.TryAddTag(Mob, tag);
            }

            // Starlight - end

            // Add item required by the trait
            if (traitPrototype.TraitGear == null)
                continue;

            if (!TryComp(Mob, out HandsComponent? handsComponent)) //Starlight
                continue;

            var coords = Transform(Mob).Coordinates; //Starlight
            var inhandEntity = Spawn(traitPrototype.TraitGear, coords);
            _sharedHandsSystem.TryPickup(Mob, //Starlight
                inhandEntity,
                checkActionBlocker: false,
                handsComp: handsComponent);
        }
    }

    // Starlight - start: Helper to detect existing lung type
    private string? GetLungAlert(EntityUid mob)
    {
        if (!TryComp<BodyComponent>(mob, out var body))
            return null;

        // Look through all body parts for lungs
        foreach (var (partId, part) in _bodySystem.GetBodyChildren(mob, body))
        {
            if (!TryComp<Robust.Shared.Containers.ContainerManagerComponent>(partId, out var containerManager))
                continue;

            // Check all containers in this body part for organs
            foreach (var container in containerManager.GetAllContainers())
            {
                foreach (var organ in container.ContainedEntities)
                {
                    if (TryComp<LungComponent>(organ, out var lung))
                    {
                        return lung.Alert;
                    }
                }
            }
        }

        return null;
    }
    // Starlight - end
}
