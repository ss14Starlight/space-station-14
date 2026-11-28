// ReSharper disable CheckNamespace
namespace Content.Server.Nuke;

public sealed partial class NukeSystem
{
    public void SetNukeTimer(EntityUid nuke, int seconds, NukeComponent? component = null)
    {
        if (!Resolve(nuke, ref component))
            return;

        component.Timer = seconds;
        component.RemainingTime = seconds;
    }
}
