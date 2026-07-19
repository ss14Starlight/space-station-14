using Content.Shared._Sol.Medical.Virology.Components;
using Content.Shared.Inventory;
using Content.Shared.Station;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Sol.Medical.Virology;

/// <summary>
/// Shared helpers for Sol pathogen / immunity / PPE queries.
/// </summary>
public abstract partial class SharedPathogenSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager PrototypeManager = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedStationSystem Station = default!;
    [Dependency] protected PathogenStrainRegistrySystem StrainRegistry = default!;

    public static readonly SlotFlags ProtectiveSlots =
        SlotFlags.OUTERCLOTHING | SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.GLOVES | SlotFlags.EYES;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PathogenResistanceComponent, InventoryRelayedEvent<PathogenResistanceQueryEvent>>(OnResistanceQuery);
    }

    private void OnResistanceQuery(
        Entity<PathogenResistanceComponent> ent,
        ref InventoryRelayedEvent<PathogenResistanceQueryEvent> args)
    {
        args.Args.TotalCoefficient *= GetRouteCoefficient(ent.Comp, args.Args.Transmission);
    }

    private static float GetRouteCoefficient(PathogenResistanceComponent comp, PathogenTransmission transmission)
    {
        return transmission switch
        {
            PathogenTransmission.Contact => comp.ContactCoefficient,
            PathogenTransmission.Airborne => comp.AirborneCoefficient,
            PathogenTransmission.Fluid => comp.FluidCoefficient,
            PathogenTransmission.Surgery => comp.SurgeryCoefficient,
            _ => 1f,
        };
    }

    public bool IsVirologyEnabledAt(EntityUid entity)
    {
        return TryGetVirologyStation(entity, out _, out _);
    }

    public bool TryGetVirologyStation(EntityUid entity, out EntityUid stationUid, out VirologyStationComponent stationComp)
    {
        stationUid = default;
        stationComp = default!;

        // Direct station entity, or owning station via tracker/grid membership.
        if (TryComp(entity, out VirologyStationComponent? self) && HasComp<Content.Shared.Station.Components.StationDataComponent>(entity))
        {
            stationUid = entity;
            stationComp = self;
            return true;
        }

        if (TryComp<Content.Shared.Station.Components.StationMemberComponent>(entity, out var member) &&
            member.Station.IsValid() &&
            TryComp(member.Station, out VirologyStationComponent? memberStation))
        {
            stationUid = member.Station;
            stationComp = memberStation;
            return true;
        }

        var station = Station.GetOwningStation(entity);
        if (station == null || !TryComp(station.Value, out VirologyStationComponent? comp))
            return false;

        stationUid = station.Value;
        stationComp = comp;
        return true;
    }

    public bool IsPathogenEnabled(EntityUid entity, PathogenPrototype pathogen)
    {
        return IsPathogenEnabled(entity, PathogenDefinition.FromPrototype(pathogen));
    }

    public bool IsPathogenEnabled(EntityUid entity, PathogenDefinition pathogen)
    {
        if (!pathogen.RequiresVirologyStation)
            return true;

        if (!TryGetVirologyStation(entity, out _, out var station))
            return false;

        if (station.EnabledPathogens.Count == 0)
            return true;

        foreach (var id in station.EnabledPathogens)
        {
            if (id == pathogen.Id || id == pathogen.ChassisId)
                return true;
        }

        return false;
    }

    public float GetImmunityMultiplier(EntityUid entity, PathogenPrototype pathogen)
    {
        return GetImmunityMultiplier(entity, PathogenDefinition.FromPrototype(pathogen));
    }

    public float GetImmunityMultiplier(EntityUid entity, PathogenDefinition pathogen)
    {
        if (!TryComp<ImmunityComponent>(entity, out var immunity))
            return 1f;

        var identity = string.IsNullOrEmpty(pathogen.VaccineIdentity) ? pathogen.Id : pathogen.VaccineIdentity;
        var best = 1f;
        var now = Timing.CurTime;

        foreach (var entry in immunity.Entries)
        {
            if (entry.ExpiresAt <= now)
                continue;

            if (entry.Identity != identity && entry.Identity != pathogen.Id && entry.Identity != pathogen.ChassisId)
                continue;

            best = Math.Min(best, entry.Strength);
        }

        return best;
    }

    public float GetPpeCoefficient(EntityUid entity, PathogenTransmission transmission)
    {
        if (!TryComp<InventoryComponent>(entity, out _))
            return 1f;

        var ev = new PathogenResistanceQueryEvent(ProtectiveSlots, transmission);
        RaiseLocalEvent(entity, ev);

        // Seal pairing: biosuit (outer) + hood (head) both RequireSeal for full protection.
        if (transmission is PathogenTransmission.Airborne or PathogenTransmission.Fluid)
        {
            var inventory = EntityManager.System<InventorySystem>();
            var hasSuit = inventory.TryGetSlotEntity(entity, "outerClothing", out var suit) &&
                          TryComp<PathogenResistanceComponent>(suit, out var suitRes) &&
                          suitRes.RequiresSeal;
            var hasHood = inventory.TryGetSlotEntity(entity, "head", out var hood) &&
                          TryComp<PathogenResistanceComponent>(hood, out var hoodRes) &&
                          hoodRes.RequiresSeal;

            ev.HasSealedSuit = hasSuit;
            ev.HasSealedHood = hasHood;

            if (!hasSuit || !hasHood)
            {
                // Incomplete seal leaks: reduce protection toward unsealed.
                const float unsealedPenalty = 0.5f;
                ev.TotalCoefficient = 1f - (1f - ev.TotalCoefficient) * (1f - unsealedPenalty);
            }
        }

        return Math.Clamp(ev.TotalCoefficient, 0f, 1f);
    }

    public bool TryGetPathogen(string id, out PathogenPrototype? pathogen)
    {
        return PrototypeManager.TryIndex(id, out pathogen);
    }

    /// <summary>
    /// Resolves a prototype chassis or round-scoped custom strain into a unified definition.
    /// </summary>
    public bool TryResolvePathogen(string id, out PathogenDefinition? pathogen)
    {
        return StrainRegistry.TryResolve(id, out pathogen);
    }

    public ActivePathogenInfection? GetInfection(EntityUid entity, string pathogenId)
    {
        if (!TryComp<PathogenCarrierComponent>(entity, out var carrier))
            return null;

        foreach (var infection in carrier.Infections)
        {
            if (infection.PathogenId == pathogenId)
                return infection;
        }

        return null;
    }

    public void AddOrIncreaseContamination(EntityUid entity, string pathogenId, float load)
    {
        var contamination = EnsureComp<SurfaceContaminationComponent>(entity);
        contamination.IsDirty = true;

        foreach (var entry in contamination.Contaminants)
        {
            if (entry.PathogenId != pathogenId)
                continue;

            entry.Load += load;
            Dirty(entity, contamination);
            return;
        }

        contamination.Contaminants.Add(new PathogenContaminationEntry
        {
            PathogenId = pathogenId,
            Load = load,
        });
        Dirty(entity, contamination);
    }

    public float GetTotalContamination(EntityUid entity, string? pathogenId = null)
    {
        if (!TryComp<SurfaceContaminationComponent>(entity, out var contamination))
            return 0f;

        var total = 0f;
        foreach (var entry in contamination.Contaminants)
        {
            if (pathogenId != null && entry.PathogenId != pathogenId)
                continue;
            total += entry.Load;
        }

        return total;
    }

    public void GrantImmunity(EntityUid entity, string identity, float strength, TimeSpan duration)
    {
        var immunity = EnsureComp<ImmunityComponent>(entity);
        var expires = Timing.CurTime + duration;

        for (var i = 0; i < immunity.Entries.Count; i++)
        {
            var entry = immunity.Entries[i];
            if (entry.Identity != identity)
                continue;

            entry.Strength = Math.Min(entry.Strength, strength);
            entry.ExpiresAt = expires > entry.ExpiresAt ? expires : entry.ExpiresAt;
            immunity.Entries[i] = entry;
            Dirty(entity, immunity);
            return;
        }

        immunity.Entries.Add(new ImmunityEntry
        {
            Identity = identity,
            Strength = strength,
            ExpiresAt = expires,
        });
        Dirty(entity, immunity);
    }
}
