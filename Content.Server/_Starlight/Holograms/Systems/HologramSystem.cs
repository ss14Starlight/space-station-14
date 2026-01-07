using Content.Shared.Popups;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Administration.Logs;
using Content.Shared.Mind.Components;
using Robust.Shared.Audio.Systems;
using Content.Shared.Database;
using Robust.Shared.Player;
using Content.Server.Humanoid;
using Content.Server.Jobs;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.Roles.Jobs;
using Content.Server.Clothing.Systems;
using Content.Shared.Preferences;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Clothing.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects.Components.Localization;
using System.Diagnostics.CodeAnalysis;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Content.Server.Access.Systems;
using Content.Server.Station.Systems;
using Content.Server.Station.Components;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Holograms;

public sealed class HologramSystem : SharedHologramSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GrammarSystem _grammar = default!;
    [Dependency] private readonly IPlayerManager _playerManager = null!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly AccessSystem _access = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly OutfitSystem _outfit = default!;

    public readonly Dictionary<EntityUid, EntityUid> HologramsWaitingForMind = new();
    /// <summary>
    ///     Handles killing a Hologram, with no checks in place.
    /// </summary>
    /// <remarks>
    ///     You should generally use <see cref="SharedHologramSystem.TryKillHologram"/> instead.
    /// </remarks>
    public override void DoKillHologram(EntityUid hologram, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return;

        var meta = MetaData(hologram);
        var holoPos = Transform(hologram).Coordinates;

        _audio.PlayPvs(holoComp.OffSound, hologram);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDisappearOther, ("name", meta.EntityName)), holoPos, Filter.PvsExcept(hologram), false, PopupType.MediumCaution);
        _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDeathSelf), holoPos, hologram, PopupType.LargeCaution);

        _entityManager.QueueDeleteEntity(hologram);

        _adminLogger.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(hologram):mob} was killed!");
    }

    public bool TryGenerateHumanoidHologram(EntityUid mindId, EntityCoordinates coords, [NotNullWhen(true)] out EntityUid? holo, bool promptConsent = false)
    {
        holo = null;

        // Get the mind component
        if (!TryComp<MindComponent>(mindId, out var mind))
            return false;

        // Check if this mind already has a hologram waiting
        if (HologramsWaitingForMind.TryGetValue(mindId, out var clone))
        {
            if (EntityManager.EntityExists(clone) &&
                !_mobState.IsDead(clone) &&
                TryComp<MindContainerComponent>(clone, out var cloneMindComp) &&
                (cloneMindComp.Mind == null || cloneMindComp.Mind == mindId))
                return false; // Mind already has clone

            HologramsWaitingForMind.Remove(mindId);
        }

        // Check if the body is alive - only dead bodies can become holograms
        if (mind.OwnedEntity != null && (_mobState.IsAlive(mind.OwnedEntity.Value) || _mobState.IsCritical(mind.OwnedEntity.Value)))
            return false; // Body controlled by mind is not dead

        // Yes, we still need to track down the client because we need to open the Eui
        if (mind.UserId == null || !_playerManager.TryGetSessionById(mind.UserId.Value, out var client))
            return false; // If we can't track down the client, we can't offer transfer. That'd be quite bad.

        // Try to get the profile from the original dead body
        HumanoidCharacterProfile? pref = null;
        if (mind.OwnedEntity != null && TryComp<HumanoidAppearanceComponent>(mind.OwnedEntity.Value, out var bodyAppearance))
        {
            pref = _humanoid.GetBaseProfile((mind.OwnedEntity.Value, bodyAppearance));
        }

        // If we can't get the body's profile, fall back to a random profile
        if (pref == null)
        {
            var prefs = _prefs.GetPreferences(mind.UserId.Value);
            pref = prefs.GetRandomEnabledProfile();
            if (pref == null)
                return false; // No valid profile found
        }

        var mob = HoloFetchAndSpawn(pref, coords, "MobHologramHardlight");

        // Only prompt for consent if requested
        // When spawning from disk, consent was already obtained when saving to disk
        if (promptConsent)
        {
            HologramsWaitingForMind.Add(mindId, mob);
            // Send popup to request consent
            _popup.PopupEntity(Loc.GetString("hologram-transfer-consent-request"), mob, client);
        }
        else
        {
            // Transfer mind immediately without prompt
            _mind.TransferTo(mindId, mob, ghostCheckOverride: true);
            _mind.UnVisit(mindId);
        }

        // Try to get the job for this mind
        if (_job.MindTryGetJob(mindId, out var jobPrototype))
        {
            foreach (var special in jobPrototype.Special)
                if (special is AddComponentSpecial)
                    special.AfterEquip(mob);

            // Get each access from the job prototype and add it to the mob
            var extended = _station.GetOwningStation(mob) is { } station && TryComp<StationJobsComponent>(station, out var jobComp) && jobComp.ExtendedAccess;
            if (TryComp<AccessComponent>(mob, out var access))
                _access.SetAccessToJob(mob, jobPrototype, extended, access);

            // Get the loadout from the job prototype and add it to the Hologram making each item unremovable.
            if (jobPrototype.StartingGear != null)
            {
                _outfit.SetOutfit(mob, jobPrototype.StartingGear, (_, item) =>
                {
                    if (TryComp<ClothingComponent>(item, out var clothing))
                    {
                        if (clothing.InSlot is "back" or "pocket1" or "pocket2" or "belt" or "suitstorage" or "id")
                        {
                            QueueDel(item);
                            return;
                        }
                    }
                    
                    // Only add HologramComponent to items (not UnremoveableComponent - that's handled by unremovable: true)
                    if (!HasComp<HologramComponent>(item))
                        AddComp<HologramComponent>(item);
                }, unremovable: true); // Set to true so UnremoveableComponent is added during spawn, not after
            }
        }

        _adminLogger.Add(LogType.Mind, LogImpact.Medium,
            $"Hologram {ToPrettyString(mob):mob} was generated at {coords}");

        holo = mob;
        return true;
    }

    internal void TransferMindToHologram(EntityUid mindId)
    {
        if (!HologramsWaitingForMind.TryGetValue(mindId, out var entity) ||
            !EntityManager.EntityExists(entity) ||
            !TryComp<MindContainerComponent>(entity, out var mindComp) ||
            mindComp.Mind != null)
            return;

        _mind.TransferTo(mindId, entity, ghostCheckOverride: true);
        _mind.UnVisit(mindId);

        HologramsWaitingForMind.Remove(mindId);
    }

    /// <summary>
    ///     Handles fetching the mob and any appearance stuff...
    /// </summary>
    private EntityUid HoloFetchAndSpawn(HumanoidCharacterProfile pref, EntityCoordinates coords, string mobPrototype)
    {
        var mob = Spawn(mobPrototype, coords);
        _transform.AttachToGridOrMap(mob);

        _humanoid.LoadProfile(mob, pref);
        _meta.SetEntityName(mob, pref.Name);

        if (TryComp<GrammarComponent>(mob, out var grammar))
        {
            _grammar.SetProperNoun((mob, grammar), true);
            _grammar.SetGender((mob, grammar), Gender.Neuter);
        }

        return mob;
    }
}
