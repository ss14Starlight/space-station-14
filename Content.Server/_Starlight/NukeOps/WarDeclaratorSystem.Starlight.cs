using System.Linq;
using Content.Shared.NukeOps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;

// ReSharper disable once CheckNamespace
namespace Content.Server.NukeOps;

public sealed partial class WarDeclaratorSystem
{
    /// <summary>
    /// True once war has been declared. Checks declaration to prevent
    /// re-firing the announcement, war music, or gamma alert.
    /// </summary>
    private bool HasWarBeenDeclared(Entity<WarDeclaratorComponent> ent)
        => ent.Comp.CurrentStatus == WarConditionStatus.WarReady;

    /// <summary>
    /// War music and gamma alert played after the war declaration announcement.
    /// </summary>
    private void PlayWarDeclarationEffects(Entity<WarDeclaratorComponent> ent)
    {
        _audio.PlayGlobal(_audio.ResolveSound(ent.Comp.WarMusic), Filter.Broadcast(), true, AudioParams.Default.WithVolume(-5f));
        if (ent.Comp.GammaAlert)
            if (_station.GetStations().FirstOrNull() is { } station)
                _alertLevel.SetLevel(station, "gamma", false, true, true, true);
    }
}
