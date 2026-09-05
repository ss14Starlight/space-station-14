using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.RedundantMovement;

[CVarDefs]
public static class RedundantMovementCVars
{
    public readonly static CVarDef<bool> Enabled = CVarDef.Create("net.redundant.enabled", true, flag: CVar.SERVER | CVar.REPLICATED,
        desc: "Whether to enable redundant movement sync");

    public readonly static CVarDef<int> MaxHistoryTicks = CVarDef.Create("net.redundant.max_history_ticks", 5, flag: CVar.SERVER | CVar.REPLICATED,
        desc: "Sets a limit on how far back the client will send redundant ticks for if it hasn't seen an ack from the server. This doesn't need to be set very high because if the ticks arrive late on the server they'll be discarded anyway, so this really only needs to be enough to reliably cover the prediction headroom / jitter buffer.");
}
