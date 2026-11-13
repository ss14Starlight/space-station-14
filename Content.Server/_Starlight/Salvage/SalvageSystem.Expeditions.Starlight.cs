// ReSharper disable CheckNamespace

using Content.Server.Salvage.Expeditions;
using Content.Shared.Salvage.Expeditions;

namespace Content.Server.Salvage;

public sealed partial class SalvageSystem
{
    private bool TryRejectFailedExpedition(SalvageExpeditionComponent expedition)
    {
        if (!expedition.GenerationFailed)
            return false;

        if (TryComp<SalvageExpeditionDataComponent>(expedition.Station, out var data))
        {
            data.ActiveMission = 0;
            data.Cooldown = false;
            data.NextOffer = _timing.CurTime;
            UpdateConsoles((expedition.Station, data));
        }

        return true;
    }
}
