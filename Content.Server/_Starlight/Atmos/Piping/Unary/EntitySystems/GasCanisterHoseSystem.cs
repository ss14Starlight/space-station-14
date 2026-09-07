using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared._Starlight.Atmos.Piping.Unary.Systems;
using GasCanisterComponent = Content.Shared.Atmos.Piping.Unary.Components.GasCanisterComponent;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Starlight.Atmos.Piping.Unary.EntitySystems;

public sealed partial class GasCanisterHoseSystem : SharedGasCanisterHoseSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private GasTankSystem _gasTank = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    protected override void RefillTank(Entity<GasCanisterComponent> canister, EntityUid tankUid, EntityUid user)
    {
        if (!TryComp<GasTankComponent>(tankUid, out var tank) || tank.IsValveOpen)
            return;

        var previousMoles = tank.Air.TotalMoles;
        _atmosphere.ReleaseGasTo(canister.Comp.Air, tank.Air, canister.Comp.ReleasePressure);
        if(tank.Air.TotalMoles <= previousMoles)
            return;

        Dirty(canister);
        tank.TotalMoles = tank.Air.TotalMoles;
        _gasTank.CheckStatus((tankUid, tank));
        _gasTank.UpdateUserInterface((tankUid, tank));
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/refill.ogg"), canister.Owner);
    }
}
