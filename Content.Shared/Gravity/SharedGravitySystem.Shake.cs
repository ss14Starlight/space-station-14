//Starlight begin | ES Screenshake
using Content.Shared._Starlight.Camera;
using Content.Shared.GameTicking;
using Robust.Shared.Player;
//Starlight end

namespace Content.Shared.Gravity;

public abstract partial class SharedGravitySystem
{
    [Dependency] private readonly ScreenshakeSystem _shake = default!; // Starlight | ES Screenshake
    [Dependency] private readonly SharedGameTicker _ticker = default!; // Starlight
    
    protected const float GravityKick = 100.0f;
    protected const float ShakeCooldown = 0.2f;

    private void UpdateShake()
    {
        var curTime = Timing.CurTime;
        var gravityQuery = GetEntityQuery<GravityComponent>();
        var query = EntityQueryEnumerator<GravityShakeComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextShake <= curTime)
            {
                if (comp.ShakeTimes == 0 || !gravityQuery.TryGetComponent(uid, out var gravity))
                {
                    RemCompDeferred<GravityShakeComponent>(uid);
                    continue;
                }

                ShakeGrid(uid, gravity);
                comp.ShakeTimes--;
                comp.NextShake += TimeSpan.FromSeconds(ShakeCooldown);
                Dirty(uid, comp);
            }
        }
    }

    public void StartGridShake(EntityUid uid, GravityComponent? gravity = null)
    {
        if (Terminating(uid))
            return;

        if (!Resolve(uid, ref gravity, false))
            return;
        
        //Starlight begin
        // I hate this stupid fucking shake at roundstart. Unfortunately, this is the definitively laziest way to do it.
        // TODO: find a less lazy way to do this. Tried checking MapComponent.MapInitialized since ComponentInit fires before that but it did not work.
        if (Timing.CurTime - _ticker.RoundStartTimeSpan < TimeSpan.FromSeconds(10))
            return;
        //Starlight end

        //Starlight begin | ES Screenshake
        var shakeParams = new ScreenshakeParameters
        {
            Trauma = 0.8f,
            DecayRate = 0.04f,
            Frequency = 0.015f
        };
        var filter = Filter.BroadcastGrid(uid);
        _shake.Screenshake(filter, shakeParams, null);
        //Starlight end
        
        if (!TryComp<GravityShakeComponent>(uid, out var shake))
        {
            shake = AddComp<GravityShakeComponent>(uid);
            shake.NextShake = Timing.CurTime;
        }

        shake.ShakeTimes = 10;
        Dirty(uid, shake);
    }

    protected virtual void ShakeGrid(EntityUid uid, GravityComponent? comp = null) {}
}
