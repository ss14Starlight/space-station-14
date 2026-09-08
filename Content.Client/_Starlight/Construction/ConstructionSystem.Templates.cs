using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client._Starlight.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

// ReSharper disable CheckNamespace
// Partial of the upstream ConstructionSystem, so it has to share its namespace.
namespace Content.Client.Construction;

public sealed partial class ConstructionSystem
{
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private ConstructionCommentSystem _comment = default!;

    /// <summary>
    /// Builds a template out of the construction ghosts on the player's current map or grid.
    /// </summary>
    /// <param name="skipped">Number of ghosts skipped because they are attached to another coordinate frame.</param>
    public ConstructionTemplate? CreateTemplate(out int skipped)
    {
        skipped = 0;

        if (_playerManager.LocalEntity is not { } user)
            return null;

        var userXform = Transform(user);

        if (userXform.MapUid is not { } map || (userXform.GridUid ?? userXform.MapUid) is not { } frame)
            return null;

        var placements = new List<(ProtoId<ConstructionPrototype> Recipe, Vector2 Position, Angle Rotation, string Comment)>();

        foreach (var ghost in _ghosts.Values)
        {
            if (!TryComp<ConstructionGhostComponent>(ghost, out var comp) || comp.Prototype is null)
                continue;

            var xform = Transform(ghost);
            var ghostFrame = xform.GridUid ?? xform.MapUid;

            if (ghostFrame != frame)
            {
                skipped++;
                continue;
            }

            var local = _transformSystem.ToCoordinates(frame, _transformSystem.GetMapCoordinates(ghost, xform));
            var rotation = _transformSystem.GetWorldRotation(xform) - _transformSystem.GetWorldRotation(frame);

            placements.Add((comp.Prototype.ID, local.Position, rotation, _comment.GetComment(ghost)));
        }

        if (placements.Count == 0)
            return null;

        var anchor = placements.MinBy(placement => (placement.Position.Y, placement.Position.X)).Position;
        var origin = new Vector2(MathF.Floor(anchor.X) + 0.5f, MathF.Floor(anchor.Y) + 0.5f);

        var template = new ConstructionTemplate
        {
            MapName = Name(map),
            OnGrid = HasComp<MapGridComponent>(frame),
            Origin = origin,
        };

        foreach (var placement in placements)
        {
            var offset = placement.Position - origin;

            template.Entries.Add(new ConstructionTemplateEntry
            {
                Recipe = placement.Recipe,
                Offset = new Vector2(MathF.Round(offset.X, 3), MathF.Round(offset.Y, 3)),
                Direction = placement.Rotation.GetDir(),
                Comment = placement.Comment,
            });
        }

        return template;
    }

    /// <summary>
    /// Gets the location a template was saved at, if the player is currently on the map it was saved on.
    /// </summary>
    public bool TryGetTemplateOrigin(ConstructionTemplate template, out EntityCoordinates origin)
    {
        origin = default;

        if (_playerManager.LocalEntity is not { } user)
            return false;

        var xform = Transform(user);

        if (string.IsNullOrEmpty(template.MapName)
            || xform.MapUid is not { } mapUid
            || Name(mapUid) != template.MapName)
        {
            return false;
        }

        EntityUid frame;

        if (template.OnGrid)
        {
            if (xform.GridUid is not { } grid)
                return false;

            frame = grid;
        }
        else
            frame = mapUid;

        origin = new EntityCoordinates(frame, template.Origin);
        return true;
    }

    /// <summary>
    /// Places every ghost of a template, with its origin at the given location.
    /// </summary>
    public int SpawnTemplate(ConstructionTemplate template, EntityCoordinates loc, Direction dir)
    {
        var rotation = dir.ToAngle();
        var placed = 0;

        foreach (var entry in template.Entries)
        {
            if (!PrototypeManager.TryIndex(entry.Recipe, out ConstructionPrototype? prototype))
                continue;

            var coords = loc.Offset(rotation.RotateVec(entry.Offset));

            if (!TrySpawnGhost(prototype, coords, (entry.Direction.ToAngle() + rotation).GetDir(), out var ghost))
                continue;

            placed++;

            if (entry.Comment.Length > 0)
                _comment.SetComment(ghost.Value, entry.Comment);
        }

        if (placed != template.Entries.Count && _playerManager.LocalEntity is { } user)
        {
            _popupSystem.PopupEntity(Loc.GetString("construction-template-partial-placement",
                    ("placed", placed),
                    ("total", template.Entries.Count)),
                user);
        }

        return placed;
    }

    /// <summary>
    /// Serializes a construction template for export.
    /// </summary>
    public DataNode ToDataNode(ConstructionTemplate template)
        => _serialization.WriteValue(template, alwaysWrite: true, notNullableOverride: true);

    /// <summary>
    /// Reads and validates a construction template from YAML.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the file does not contain one supported template.</exception>
    public ConstructionTemplate FromStream(Stream stream)
    {
        using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        if (yamlStream.Documents.Count != 1)
            throw new InvalidDataException("A construction template must contain exactly one YAML document.");

        var root = yamlStream.Documents[0].RootNode;
        var template = _serialization.Read<ConstructionTemplate>(root.ToDataNode(), notNullableOverride: true);

        if (template.Version != ConstructionTemplate.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported construction template version {template.Version}; expected {ConstructionTemplate.CurrentVersion}.");
        }

        foreach (var entry in template.Entries)
        {
            if (!float.IsFinite(entry.Offset.X) || !float.IsFinite(entry.Offset.Y))
                throw new InvalidDataException("Construction template offsets must be finite numbers.");

            if (entry.Direction is not (Direction.South or Direction.East or Direction.North or Direction.West))
                throw new InvalidDataException("Construction template directions must be cardinal.");
        }

        return template;
    }
}
