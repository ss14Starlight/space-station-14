using System.Linq;
using Content.Shared.Buckle.Components;
using Content.Shared.GameTicking;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Camera;

public sealed class ShakeOnStairsSystem : EntitySystem
{
    [Dependency] private readonly ScreenshakeSystem _shake = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _shakeCooldowns = [];
    private readonly Dictionary<EntityUid, MapCoordinates> _lastShakeCoords = [];
    private static readonly ProtoId<TagPrototype> StairTag = new("Stairs");

    public override void Initialize()
    {
        base.Initialize();

        _xform.OnGlobalMoveEvent += OnMoveEvent;
        SubscribeLocalEvent<EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }
    
    private void OnMoveEvent(ref MoveEvent ev)
    {
        // This is probably extremely inefficient, but I can't think of a better way to do this.
        if (!TryComp<StrapComponent>(ev.Sender, out _)) return;
        var query = EntityQueryEnumerator<BuckleComponent>();
        while (query.MoveNext(out var uid, out var buckle))
        {
            if (!buckle.Buckled) continue;
            if (buckle.BuckledTo != ev.Sender) continue;
            var inRange = _lookup.GetEntitiesInRange(buckle.BuckledTo.Value, 0.5f);
            foreach (var _ in inRange.Where(x => _tag.HasTag(x, StairTag)))
            {
                if (_shakeCooldowns.ContainsKey(uid)) continue;
                var currentCoords = _xform.GetMapCoordinates(uid);
                if(_lastShakeCoords.TryGetValue(uid, out var coords))
                    if (currentCoords.InRange(coords, 0.22f)) // to prevent slight movements from causing screenshake
                        continue;
                _shakeCooldowns[uid] = _timing.CurTime + TimeSpan.FromSeconds(0.05f); // just to prevent this from happening every frame
                _lastShakeCoords[uid] = currentCoords;
                var translation = new ScreenshakeParameters
                {
                    Trauma = 0.4f,
                    DecayRate = 1.8f,
                    Frequency = 0.02f,
                };
                var rotation = new ScreenshakeParameters
                {
                    Trauma = 0.14f,
                    DecayRate = 1.2f,
                    Frequency = 0.013f,
                };
                _shake.Screenshake(uid, translation, rotation);
            }
        }
    }

    private void OnTerminating(ref EntityTerminatingEvent ev)
    {
        _shakeCooldowns.Remove(ev.Entity);
        _lastShakeCoords.Remove(ev.Entity);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        _shakeCooldowns.Clear();
        _lastShakeCoords.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var shaker in _shakeCooldowns.Where(shaker => shaker.Value < _timing.CurTime))
            _shakeCooldowns.Remove(shaker.Key);
    }
}