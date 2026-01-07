using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Map;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Storage.Components;
using Content.Shared.Database;
using Content.Shared._Starlight.Holograms.Components;
using Robust.Shared.Configuration;
using Content.Shared.Whitelist;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Shared._Starlight.Holograms;

public partial class SharedHologramSystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    private void InitializeProjected() =>
        SubscribeLocalEvent<HologramComponent, EntityStorageInsertedIntoAttemptEvent>(OnStoreInContainerAttempt);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = _entityManager.EntityQueryEnumerator<HologramProjectedComponent>();
        while (query.MoveNext(out var hologram, out var hologramProjectedComp))
        {
            // Skip client-side entities (like spawn menu previews)
            if (_entityManager.IsClientSide(hologram))
                continue;
                
            ProjectedUpdate(hologram, hologramProjectedComp);
        }
    }

    /// <summary>
    ///     Returns a hologram to its last visited projector, or kills it if the projector is invalid.
    /// </summary>
    public virtual void DoReturnHologram(EntityUid hologram, HologramProjectedComponent? holoProjectedComp = null)
    {
        if (!Resolve(hologram, ref holoProjectedComp))
            return;

        // Don't process if entity is terminating
        if (Terminating(hologram))
            return;

        // If their last visited Projector is invalid ignoring occlusion and none is found
        EntityUid? curProjectorEntity = null;
        if (holoProjectedComp.CurProjector != null)
            TryGetEntity(holoProjectedComp.CurProjector.Value, out curProjectorEntity);

        if (!IsHoloProjectorValid(hologram, curProjectorEntity, false) &&
            !TryGetHoloProjector(hologram, out curProjectorEntity, holoProjectedComp, false))
        {
            // Kill the hologram.
            TryKillHologram(hologram);
            return;
        }

        holoProjectedComp.CurProjector = GetNetEntity(curProjectorEntity.Value);
        Dirty(hologram, holoProjectedComp);

        var returnedEvent = new HologramReturnAttemptEvent();
        RaiseLocalEvent(hologram, ref returnedEvent);
        if (returnedEvent.Cancelled)
            return;

        RaiseLocalEvent(hologram, new HologramReturnedEvent(curProjectorEntity.Value));

        MoveHologramToProjector(hologram, curProjectorEntity.Value);

        _adminLogger.Add(LogType.Mind, LogImpact.Low,
            $"{ToPrettyString(hologram):mob} was returned to projector {ToPrettyString(holoProjectedComp.CurProjector.Value):entity}");
    }

    /// <summary>
    ///     Tests for the nearest projector to a set of coords.
    /// </summary>
    /// <param name="coords">The coords to perform the check from.</param>
    /// <param name="result">The UID of the projector, or null if no projectors are found.</param>
    /// <param name="whiteList">An EntityWhitelist to check for on projectors to determine if they're valid.</param>
    /// <param name="occlude">Should it check only for unoccluded and in range projectors?</param>
    /// <returns>Returns true if a projector is found, false if not.</returns>
    public bool TryGetHoloProjector(MapCoordinates coords, [NotNullWhen(true)] out EntityUid? result, EntityWhitelist? whiteList = null, bool occlude = true)
    {
        result = null;

        // Sort all projectors in distance increasing order.
        var nearProjList = new SortedList<float, EntityUid>();

        var query = _entityManager.EntityQueryEnumerator<HologramProjectorComponent>();
        while (query.MoveNext(out var projector, out var projComp))
        {
            // Skip inactive projectors
            if (!projComp.IsActive)
                continue;
                
            var dist = (_transform.GetWorldPosition(projector) - coords.Position).LengthSquared();
            nearProjList.TryAdd(dist, projector);
        }

        // Find the nearest, valid projector.
        foreach (var nearProj in nearProjList)
        {
            if (!IsHoloProjectorValid(coords, nearProj.Value, occlude, whiteList))
                continue;
            result = nearProj.Value;
            return true;
        }
        return false;
    }

    /// <remarks>
    ///     This takes into consideration any ProjectorOverride the hologram may have.
    /// </remarks>
    /// <inheritdoc cref="TryGetHoloProjector"/>
    public bool TryGetHoloProjector(EntityUid uid, [NotNullWhen(true)] out EntityUid? result, HologramProjectedComponent? projectedComp = null, bool occlude = true)
    {
        result = null;

        if (!Resolve(uid, ref projectedComp))
            return false;

        if (projectedComp.ProjectorOverride != null) // Check for Component-set overrides.
        {
            if (TryGetEntity(projectedComp.ProjectorOverride.Value, out var overrideEntity))
            {
                if (IsHoloProjectorValid(uid, overrideEntity, occlude))
                {
                    result = overrideEntity;
                    return true;
                }
            }
            return false;
        }

        var projectorEvent = new HologramGetProjectorEvent(); // Check for Event-set overrides.
        RaiseLocalEvent(uid, ref projectorEvent);
        if (projectorEvent.Override)
        {
            result = projectorEvent.ProjectorOverride;
            return projectorEvent.ProjectorOverride != null;
        }

        // Otherwise, we simply check for the nearest projector, considering any tags it requires.
        return TryGetHoloProjector(_transform.GetMapCoordinates(uid), out result, projectedComp.ValidProjectorWhitelist, occlude);
    }

    /// <summary>
    ///     Tests if a projector is valid for a given hologram.
    /// </summary>
    /// <param name="hologram">The hologram to check for, or its position.</param>
    /// <param name="projector">The projector to compare on, or its position.</param>
    /// <param name="occlude">Should it check only for unoccluded and in range projectors?</param>.
    /// <param name="raiseEvent">Should it raise the <see cref="HologramCheckProjectorValidEvent"/> event? Make sure this is set to false if you use this function in response to the event.</param>
    /// <param name="projectedComp">The hologram's component. If provided, the hologram's list of allowed tags will be used.</param>
    /// <returns>True if the projector is within range, and unoccluded to the hologram. Otherwise, false.</returns>
    public bool IsHoloProjectorValid(EntityUid hologram, [NotNullWhen(true)] EntityUid? projector, bool occlude = true, bool raiseEvent = true, HologramProjectedComponent? projectedComp = null)
    {
        if (!Resolve(hologram, ref projectedComp) || projector == null || !Exists(projector.Value))
            return false;

        if (raiseEvent)
        {
            var validCheckEvent = new HologramCheckProjectorValidEvent(projector.Value);
            RaiseLocalEvent(hologram, ref validCheckEvent);
            if (validCheckEvent.Valid != null)
                return validCheckEvent.Valid.Value;
        }

        return IsHoloProjectorValid(_transform.GetMapCoordinates(hologram), projector, occlude, projectedComp.ValidProjectorWhitelist);
    }

    /// <inheritdoc cref="IsHoloProjectorValid"/>
    /// <param name="whitelist">A whitelist to check for on projectors, to determine if they're valid. Usually found on the Holo's <see cref="HologramProjectedComponent"/>.</param>
    /// <remarks>
    ///     Note this this method won't raise the <see cref="HologramCheckProjectorValidEvent"/> event, as the Hologram entity is not known.
    ///     This is a limitation of the method, and should be kept in mind when using it.
    /// </remarks> //TODO: HOLO Probably allow passing in a nullable UID for the hologram, and raise the event if it's not null.
    public bool IsHoloProjectorValid(MapCoordinates hologram, [NotNullWhen(true)] EntityUid? projector, bool occlude = true, EntityWhitelist? whitelist = null)
    {
        if (projector == null || !Exists(projector.Value))
            return false;

        if (!TryComp<HologramProjectorComponent>(projector.Value, out var projComp))
            return false;

        if (!projComp.IsActive)
            return false;

        if (whitelist != null && !_whitelist.IsValid(whitelist, projector.Value))
            return false;

        // Get the projector's range
        var range = projComp.ProjectorRange;

        if (occlude && !_examine.InRangeUnOccluded(hologram, _transform.ToMapCoordinates(Transform(projector.Value).Coordinates), range, null))
            return false;

        return true;
    }

    /// <summary>
    ///     Moves a hologram to a new location.
    /// </summary>
    /// <remarks>
    ///     Does no validation for any projectors before moving.
    /// </remarks>
    /// <param name="hologram">The hologram to move.</param>
    /// <param name="projector">The projector to move it to, or the projector's position.</param>
    public void MoveHologram(EntityUid hologram, EntityCoordinates projector, HologramComponent? holoComp = null)
    {
        if (!Resolve(hologram, ref holoComp))
            return;

        // Stops any pulling goin on.
        if (TryComp<PullableComponent>(hologram, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(hologram, pullable);

        if (TryComp<PullerComponent>(hologram, out var pulling) && pulling.Pulling != null &&
            TryComp<PullableComponent>(pulling.Pulling.Value, out var subjectPulling))
            _pulling.TryStopPull(pulling.Pulling.Value, subjectPulling);

        // Plays the vanishing effects.
        var meta = MetaData(hologram);

        if (!_timing.InPrediction) // TODOPark: HOLO Change this to run on the first prediction once it predicts reliably.
        {
            var holoPos = Transform(hologram).Coordinates;
            _audio.PlayPvs(holoComp.OffSound, hologram);
            _popup.PopupCoordinates(Loc.GetString(holoComp.PopupDisappearOther, ("name", meta.EntityName)), holoPos, Filter.PvsExcept(hologram), false, PopupType.MediumCaution);
        }

        // Does the do.
        _transform.SetCoordinates(hologram, projector);
        _transform.AttachToGridOrMap(hologram);

        // Plays the appearing effects.
        if (!_timing.InPrediction)
        {
            _audio.PlayPvs(holoComp.OnSound, hologram);
            _popup.PopupEntity(Loc.GetString(holoComp.PopupAppearOther, ("name", meta.EntityName)), hologram, Filter.PvsExcept(hologram), false, PopupType.Medium);
            _popup.PopupEntity(Loc.GetString(holoComp.PopupAppearSelf, ("name", meta.EntityName)), hologram, hologram, PopupType.Large);
        }
    }

    /// <inheritdoc cref="MoveHologram"/>
    public void MoveHologramToProjector(EntityUid hologram, EntityUid projector, HologramComponent? holoComp = null) =>
        MoveHologram(hologram, Transform(projector).Coordinates, holoComp);

    protected bool ProjectedUpdate(EntityUid hologram, HologramProjectedComponent hologramProjectedComp)
    {
        if (TryGetHoloProjector(hologram, out var nearProj, hologramProjectedComp)) // Checks for a projector in range.
        {
            hologramProjectedComp.CurProjector = GetNetEntity(nearProj.Value);
            hologramProjectedComp.CurrentlyInProjector = true;
            Dirty(hologram, hologramProjectedComp);
            return true;
        }

        // If none is found, and they were in the range of a projector during the last check, we set the time they'll be disappeared at.
        if (hologramProjectedComp.CurrentlyInProjector)
        {
            hologramProjectedComp.CurrentlyInProjector = false;
            hologramProjectedComp.VanishTime = _timing.CurTime + hologramProjectedComp.GracePeriod;
        }

        if (hologramProjectedComp.VanishTime > _timing.CurTime)
        {
            Dirty(hologram, hologramProjectedComp);
            return true;
        }

        // Attempts to return the hologram if their time is up.
        DoReturnHologram(hologram);
        Dirty(hologram, hologramProjectedComp);
        return false;
    }

    // Forbid holograms from going inside anything. Osmosised from Nyano :)
    private void OnStoreInContainerAttempt(EntityUid uid, HologramComponent component, ref EntityStorageInsertedIntoAttemptEvent args)
    {
        // Don't process storage attempts for entities that are being deleted
        if (Terminating(uid))
            return;

        if (HasComp<HologramProjectedComponent>(uid))
        {
            DoReturnHologram(uid);
            args.Cancelled = true;
        }
    }
}
