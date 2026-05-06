using System.Numerics;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Antags.Vampires;

/// <summary>
/// Client-side system for smooth vampire beams visualization
/// </summary>
public sealed class VampireDrainBeamSystem : EntitySystem
{
    private enum BeamKind
    {
        Drain,
        BloodBond
    }

    private static readonly Angle _beamAngleOffset = Angle.FromDegrees(180); // suck em
    private const bool SpriteIsVertical = true;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string DrainPrototype = "VampireDrainBeamVisual";
    private const string BloodBondPrototype = "VampireBloodBondBeamVisual";

    /// <summary>
    /// Tracks client-side beam visual entities
    /// Key = (kind, source, target) pair, Value = visual beam entity
    /// </summary>
    private readonly Dictionary<(BeamKind, EntityUid, EntityUid), EntityUid> _activeBeamVisuals = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<VampireDrainBeamEvent>(OnDrainBeamEvent);
        SubscribeNetworkEvent<VampireBloodBondBeamEvent>(OnBloodBondBeamEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Update all active beam visuals every frame for smooth following
        var toRemove = new List<(BeamKind, EntityUid, EntityUid)>();

        foreach (var ((kind, source, target), beamEntity) in _activeBeamVisuals)
        {
            // Check if entities still exist
            if (!Exists(source) || !Exists(target) || !Exists(beamEntity))
            {
                toRemove.Add((kind, source, target));
                if (Exists(beamEntity))
                    QueueDel(beamEntity);
                continue;
            }

            UpdateBeamVisual(beamEntity, source, target);
        }

        foreach (var key in toRemove)
        {
            _activeBeamVisuals.Remove(key);
        }
    }

    private void OnDrainBeamEvent(VampireDrainBeamEvent ev)
        => HandleBeamEvent(ev.Source, ev.Target, ev.Create, BeamKind.Drain, DrainPrototype);

    private void OnBloodBondBeamEvent(VampireBloodBondBeamEvent ev)
        => HandleBeamEvent(ev.Source, ev.Target, ev.Create, BeamKind.BloodBond, BloodBondPrototype);

    private void HandleBeamEvent(NetEntity sourceNet, NetEntity targetNet, bool create, BeamKind kind, string prototype)
    {
        var source = GetEntity(sourceNet);
        var target = GetEntity(targetNet);

        if (!Exists(source) || !Exists(target))
            return;

        var key = (kind, source, target);

        if (create)
        {
            CreateBeamVisual(kind, prototype, source, target);
            return;
        }

        if (_activeBeamVisuals.TryGetValue(key, out var beamEntity))
        {
            QueueDel(beamEntity);
            _activeBeamVisuals.Remove(key);
        }
    }

    private void CreateBeamVisual(BeamKind kind, string prototype, EntityUid source, EntityUid target)
    {
        var key = (kind, source, target);

        // Remove existing beam if any exist
        if (_activeBeamVisuals.TryGetValue(key, out var existingBeam))
        {
            QueueDel(existingBeam);
        }

        var beam = Spawn(prototype, Transform(source).Coordinates);

        _activeBeamVisuals[key] = beam;

        UpdateBeamVisual(beam, source, target);
    }

    private void UpdateBeamVisual(EntityUid beam, EntityUid source, EntityUid target)
    {
        if (!TryComp<SpriteComponent>(beam, out var sprite))
            return;

        var sourcePos = _transform.GetWorldPosition(source);
        var targetPos = _transform.GetWorldPosition(target);

        var direction = targetPos - sourcePos;
        var distance = direction.Length();

        if (distance < 0.1f)
            return;

        var worldAngle = direction.ToWorldAngle() + _beamAngleOffset;

        var midpoint = sourcePos + (direction * 0.5f);
        _transform.SetWorldPosition(beam, midpoint);

        _transform.SetWorldRotation(beam, worldAngle);
        _sprite.SetRotation((beam, sprite), Angle.Zero);

        // Scale beam to match distance. Isvertical ? scale Y : scale X
        var length = MathF.Max(0.05f, distance);
        var thickness = 0.9f;
        var scale = SpriteIsVertical ? new Vector2(thickness, length) : new Vector2(length, thickness);
        _sprite.SetScale((beam, sprite), scale);
        _sprite.SetOffset((beam, sprite), Vector2.Zero);
    }

    public override void Shutdown()
    {
        // Clean up all beam visuals
        foreach (var beamEntity in _activeBeamVisuals.Values)
        {
            if (Exists(beamEntity))
                QueueDel(beamEntity);
        }
        _activeBeamVisuals.Clear();

        base.Shutdown();
    }
}
