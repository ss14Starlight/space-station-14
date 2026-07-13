using System.Linq;
using System.Numerics;
using Content.Shared._Starlight.Abstract.Extensions;
using Content.Shared.Random.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.CustomSpawner;

public abstract partial class SharedCustomSpawnerSystem : EntitySystem
{
    /// Color interpolation lambda
    private const float Lambda = 0.5f;

    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomSpawnerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CustomSpawnerComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
        SubscribeLocalEvent<CustomSpawnerComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CustomSpawnerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled) continue;
            if (!comp.SpawnOnInterval) continue;
            if (_timing.CurTime < comp.NextSpawnTime) continue;
            comp.NextSpawnTime = _timing.CurTime + comp.SpawnInterval;
            DoSpawn(uid, comp);
        }
    }

    protected virtual void DoSpawn(EntityUid uid, CustomSpawnerComponent comp)
    {
        if (!comp.Enabled) return;
        if (comp.SpawnData.Count <= 0) return;
        if (comp.MaxTriggers >= 0)
        {
            if(comp.TimesTriggered >= comp.MaxTriggers) return;
            comp.TimesTriggered++;
        }
        if (!_random.ProbPredicted(_timing, comp.TriggerProb)) return;
        switch (comp.SpawnStrategy)
        {
            case SpawnStrategy.All:
                SpawnAll(uid, comp);
                break;
            case SpawnStrategy.Sequential:
                SpawnSequence(uid, comp);
                break;
            case SpawnStrategy.Random:
                SpawnRandom(uid, comp);
                break;
            default:
                SpawnAll(uid, comp);
                break;
        }

        if (comp.OneShot) comp.Enabled = false;
    }

    private void SpawnAll(EntityUid uid, CustomSpawnerComponent comp)
    {
        foreach (var data in comp.SpawnData)
            SpawnFromData(uid, comp, data);
    }

    private void SpawnSequence(EntityUid uid, CustomSpawnerComponent comp)
    {
        var data = comp.SpawnData[comp.SpawnIndex++];
        if (comp.SpawnIndex == comp.SpawnData.Count) comp.SpawnIndex = 0;
        SpawnFromData(uid, comp, data);
    }

    private void SpawnRandom(EntityUid uid, CustomSpawnerComponent comp) =>
        SpawnFromData(uid, comp,
            _random.PickPredicted(_timing, comp.SpawnData.ToDictionary(data => data, data => data.PickWeight)));

    private void SpawnFromData(EntityUid uid, CustomSpawnerComponent comp, CustomSpawnData data)
    {
        var xform = Transform(uid);
        EntityCoordinates? storedPosition = null;
        ComponentRegistry? storedOverrides = null;
        float? storedRotation = null;
        var rng = _random.GetPredictedRandom(_timing);
        for (var i = 0; i < data.RepeatCount + 1; i++)
        {
            // RNG check
            if (!_random.ProbPredicted(_timing, data.SpawnProb, rng.Next()))
                continue;
            // Only consume a spawn if it passes RNG.
            if (data.MaxSpawns >= 0)
            {
                if (data.TimesSpawned >= data.MaxSpawns) return;
                data.TimesSpawned++;
            }

            if ((data.RepeatSameOffsets && storedPosition is null) || !data.RepeatSameOffsets)
                storedPosition = GetSpawnPosition(comp, data, xform, rng);
            if ((data.RepeatSameRotations && storedRotation is null) || !data.RepeatSameRotations)
                storedRotation = GetSpawnRotation(comp, data, rng);
            if ((data.RepeatSameOverrides && storedOverrides is null) || !data.RepeatSameOverrides)
                storedOverrides = data.Overrides.Count > 0
                    ? data.Overrides.Count == 1
                        ? data.Overrides.First()
                        : _random.PickPredicted(_timing, data.Overrides, rng.Next())
                    : null;
            // Impossible to get here with storedPosition being null, should be anyway.
            var spawned = PredictedSpawnAtPosition(data.ProtoId, storedPosition!.Value, storedOverrides);
            Transform(spawned).LocalRotation = float.DegreesToRadians(storedRotation!.Value);
        }
    }

    private EntityCoordinates GetSpawnPosition(CustomSpawnerComponent comp, CustomSpawnData data, TransformComponent xform, System.Random rng)
    {
        var pos = xform.Coordinates + new EntityCoordinates(xform.ParentUid, comp.GlobalSpawnOffset);
        switch (data.SpawnOffsets.Count)
        {
            case 0:
                break;
            case 1:
                pos += new EntityCoordinates(xform.ParentUid, data.SpawnOffsets.First());
                break;
            default:
                pos += new EntityCoordinates(xform.ParentUid,
                    _random.PickPredicted(_timing, data.SpawnOffsets, rng.Next()));
                break;
        }

        // apply range if enabled
        if (!data.UseOffsetRange) return pos;
        var x = _random.NextFloatPredicted(_timing, data.OffsetRangeMin.X, data.OffsetRangeMax.X, rng.Next());
        var y = _random.NextFloatPredicted(_timing, data.OffsetRangeMin.Y, data.OffsetRangeMax.Y, rng.Next());
        pos += new EntityCoordinates(xform.ParentUid, new Vector2(x, y));
        return pos;
    }

    private float GetSpawnRotation(CustomSpawnerComponent comp, CustomSpawnData data, System.Random rng)
    {
        var rotation = comp.GlobalSpawnRotation;
        switch (data.SpawnRotations.Count)
        {
            case 0:
                break;
            case 1:
                rotation += data.SpawnRotations.First();
                break;
            default:
                rotation += _random.PickPredicted(_timing, data.SpawnRotations, rng.Next());
                break;
        }

        // apply range if enabled
        if (!data.UseRotationRange) return rotation;
        rotation += _random.NextFloatPredicted(_timing, data.RotationRangeMin, data.RotationRangeMax, rng.Next());
        return rotation;
    }

    private void OnMapInit(Entity<CustomSpawnerComponent> ent, ref MapInitEvent args)
    {
        Dirty(ent); // Force dirty to trigger update on client when spawned or during mapinit because client is fucking stupid.
        var uid = ent.Owner;
        var comp = ent.Comp;
        if (comp.LightVisible)
        {
            _light.SetEnabled(uid, true);
            _light.SetColor(uid, Color.InterpolateBetween(comp.HologramColor1, comp.HologramColor2, Lambda));
        }
        else _light.SetEnabled(uid, false);
        if (comp.IsMarker) return;
        if (comp.HologramProtoId is null) return;
        comp.HologramEntity = PredictedSpawnAttachedTo(comp.HologramProtoId, Transform(uid).Coordinates);
        _xform.SetParent(comp.HologramEntity.Value, uid); // PredictedSpawnAttachedTo seems to just not work for this??? so here we are i guess
        UpdateHologram(uid, comp, comp.HologramEntity.Value,
            Comp<CustomSpawnerHologramComponent>(comp.HologramEntity.Value));
    }

    private void OnAfterAutoHandleState(Entity<CustomSpawnerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var uid = ent.Owner;
        var comp = ent.Comp;
        if (comp.LightVisible)
        {
            _light.SetEnabled(uid, true);
            _light.SetColor(uid, Color.InterpolateBetween(comp.HologramColor1, comp.HologramColor2, Lambda));
        }
        else _light.SetEnabled(uid, false);
        if (comp.IsMarker) return;
        if (TryComp<CustomSpawnerHologramComponent>(comp.HologramEntity, out var holo))
            UpdateHologram(uid, comp, comp.HologramEntity.Value, holo);
    }

    private void OnShutdown(Entity<CustomSpawnerComponent> ent, ref ComponentShutdown args)
    {
        if(ent.Comp.HologramEntity is not null)
            PredictedQueueDel(ent.Comp.HologramEntity.Value);
    }

    protected virtual void UpdateHologram(EntityUid spawner, CustomSpawnerComponent sComp, EntityUid hologram, CustomSpawnerHologramComponent hComp)
    {
        hComp.Color1 = sComp.HologramColor1;
        hComp.Color2 = sComp.HologramColor2;
        if (sComp.HologramSprite is not null)
        {
            hComp.Rsi = sComp.HologramSprite.RsiPath.ToString();
            hComp.State = sComp.HologramSprite.RsiState;
        }
        hComp.Offset = sComp.HologramOffset;
        hComp.ProtoSprite = sComp.HologramProtoSprite;
        hComp.UseProtoSprite = sComp.UseProtoSprite;
    }
}
