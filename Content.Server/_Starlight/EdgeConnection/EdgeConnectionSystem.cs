using Content.Shared._Starlight.EdgeConnection;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server._Starlight.EdgeConnection;

/// <summary>
/// Handles visual edge connections between entities placed adjacent to each other.
/// Updates appearance data based on neighboring entities with matching connection keys.
/// </summary>
public sealed class EdgeConnectionSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EdgeConnectionComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EdgeConnectionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EdgeConnectionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<EdgeConnectionComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<EdgeConnectionComponent, MoveEvent>(OnMove);
    }

    private void OnInit(Entity<EdgeConnectionComponent> ent, ref ComponentInit args)
    {
        UpdateConnections(ent);
        UpdateNeighbors(ent);
    }

    private void OnAnchorChanged(Entity<EdgeConnectionComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateConnections(ent);
        UpdateNeighbors(ent);
    }

    private void OnShutdown(Entity<EdgeConnectionComponent> ent, ref ComponentShutdown args)
    {
        // Update neighbors when this entity is removed
        UpdateNeighbors(ent);
    }

    private void OnTerminating(Entity<EdgeConnectionComponent> ent, ref EntityTerminatingEvent args)
    {
        // Update neighbors when entity is completely destroyed or deleted
        UpdateNeighbors(ent);
    }

    private void OnMove(Entity<EdgeConnectionComponent> ent, ref MoveEvent args)
    {
        // Only update if rotation changed
        if (!args.OldRotation.EqualsApprox(args.NewRotation))
        {
            UpdateConnections(ent);
            UpdateNeighbors(ent);
        }
    }

    private void UpdateConnections(Entity<EdgeConnectionComponent> ent)
    {
        var xform = Transform(ent);
        
        if (!xform.Anchored || !TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            _appearance.SetData(ent, EdgeConnectionVisuals.ConnectionMask, EdgeConnectionFlags.None);
            return;
        }

        var mask = EdgeConnectionFlags.None;
        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);
        var allowed = ent.Comp.AllowedDirections;
        var rotation = xform.LocalRotation;

        // Transform allowed directions to world space
        var worldAllowed = RotateDirections(allowed, rotation);

        // Check each world direction if it's allowed after rotation
        if ((worldAllowed & EdgeConnectionFlags.East) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(1, 0), ent.Comp.ConnectionKey, EdgeConnectionFlags.West))
                mask |= EdgeConnectionFlags.East;
        }

        if ((worldAllowed & EdgeConnectionFlags.West) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(-1, 0), ent.Comp.ConnectionKey, EdgeConnectionFlags.East))
                mask |= EdgeConnectionFlags.West;
        }

        if ((worldAllowed & EdgeConnectionFlags.North) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(0, 1), ent.Comp.ConnectionKey, EdgeConnectionFlags.South))
                mask |= EdgeConnectionFlags.North;
        }

        if ((worldAllowed & EdgeConnectionFlags.South) != 0)
        {
            if (HasMatchingNeighbor(ent, xform.GridUid.Value, grid, tile + new Vector2i(0, -1), ent.Comp.ConnectionKey, EdgeConnectionFlags.North))
                mask |= EdgeConnectionFlags.South;
        }

        // Transform mask back to local space for sprite state selection
        var localMask = RotateDirectionsInverse(mask, rotation);

        _appearance.SetData(ent, EdgeConnectionVisuals.ConnectionMask, localMask);
    }

    /// <summary>
    /// Rotates direction flags by the given angle (in 90-degree increments).
    /// Used to convert entity-local directions to world-space directions.
    /// </summary>
    private EdgeConnectionFlags RotateDirections(EdgeConnectionFlags flags, Angle rotation)
    {
        return RotateDirectionsImpl(flags, rotation, clockwise: true);
    }

    /// <summary>
    /// Rotates direction flags by the inverse of the given angle.
    /// Used to convert world-space directions back to entity-local directions.
    /// </summary>
    private EdgeConnectionFlags RotateDirectionsInverse(EdgeConnectionFlags flags, Angle rotation)
    {
        return RotateDirectionsImpl(flags, rotation, clockwise: false);
    }

    private EdgeConnectionFlags RotateDirectionsImpl(EdgeConnectionFlags flags, Angle rotation, bool clockwise)
    {
        // Normalize angle to 0-360
        var degrees = (int)Math.Round(rotation.Degrees) % 360;
        if (degrees < 0) degrees += 360;

        // Round to nearest 90 degrees
        var quarterTurns = (int)Math.Round(degrees / 90.0) % 4;
        
        // Invert if counterclockwise
        if (!clockwise)
            quarterTurns = (4 - quarterTurns) % 4;

        if (quarterTurns == 0)
            return flags;

        for (int i = 0; i < quarterTurns; i++)
        {
            var rotated = EdgeConnectionFlags.None;

            if (clockwise)
            {
                // Clockwise: North→East→South→West
                if ((flags & EdgeConnectionFlags.North) != 0) rotated |= EdgeConnectionFlags.East;
                if ((flags & EdgeConnectionFlags.East) != 0) rotated |= EdgeConnectionFlags.South;
                if ((flags & EdgeConnectionFlags.South) != 0) rotated |= EdgeConnectionFlags.West;
                if ((flags & EdgeConnectionFlags.West) != 0) rotated |= EdgeConnectionFlags.North;
            }
            else
            {
                // Counterclockwise: North→West→South→East
                if ((flags & EdgeConnectionFlags.North) != 0) rotated |= EdgeConnectionFlags.West;
                if ((flags & EdgeConnectionFlags.West) != 0) rotated |= EdgeConnectionFlags.South;
                if ((flags & EdgeConnectionFlags.South) != 0) rotated |= EdgeConnectionFlags.East;
                if ((flags & EdgeConnectionFlags.East) != 0) rotated |= EdgeConnectionFlags.North;
            }
            
            flags = rotated;
        }

        return flags;
    }

    /// <summary>
    /// Checks if there's a matching neighbor at the given tile that can connect.
    /// Neighbors must have matching connection keys, support the required direction,
    /// and have the same rotation as the source entity.
    /// </summary>
    private bool HasMatchingNeighbor(EntityUid entity, EntityUid gridUid, MapGridComponent grid, Vector2i tile, string key, EdgeConnectionFlags requiredDirection)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        var entityXform = Transform(entity);
        
        while (anchored.MoveNext(out var other))
        {
            if (other == entity)
                continue;

            if (!TryComp<EdgeConnectionComponent>(other, out var comp) || comp.ConnectionKey != key)
                continue;

            var otherXform = Transform(other.Value);
            if (!otherXform.Anchored)
                continue;

            // Check if neighbor allows connections in the required world-space direction
            var otherWorldAllowed = RotateDirections(comp.AllowedDirections, otherXform.LocalRotation);
            if ((otherWorldAllowed & requiredDirection) == 0)
                continue;

            // Only connect if both entities have the same rotation
            var entityDegrees = ((int)Math.Round(entityXform.LocalRotation.Degrees) % 360 + 360) % 360;
            var otherDegrees = ((int)Math.Round(otherXform.LocalRotation.Degrees) % 360 + 360) % 360;
            
            if (entityDegrees == otherDegrees)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Updates all neighboring entities' edge connections when this entity changes.
    /// </summary>
    private void UpdateNeighbors(Entity<EdgeConnectionComponent> ent)
    {
        var xform = Transform(ent);
        
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates);

        // Update all potentially affected neighbors
        UpdateNeighborsAtTile(xform.GridUid.Value, grid, tile + new Vector2i(1, 0));
        UpdateNeighborsAtTile(xform.GridUid.Value, grid, tile + new Vector2i(-1, 0));
        UpdateNeighborsAtTile(xform.GridUid.Value, grid, tile + new Vector2i(0, 1));
        UpdateNeighborsAtTile(xform.GridUid.Value, grid, tile + new Vector2i(0, -1));
    }

    /// <summary>
    /// Updates edge connections for all entities at a specific tile.
    /// </summary>
    private void UpdateNeighborsAtTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        
        while (anchored.MoveNext(out var other))
        {
            if (TryComp<EdgeConnectionComponent>(other, out var comp))
            {
                UpdateConnections((other.Value, comp));
            }
        }
    }
}
