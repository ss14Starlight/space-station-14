using Content.Shared.DeviceLinking.Events;
using Content.Shared.Silicons.Laws.Components;

namespace Content.Shared.Silicons.StationAi;

public abstract partial class SharedStationAiSystem
{
    // Handles device linking

    private void InitializeLinking()
    {
        SubscribeLocalEvent<StationAiCoreComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<StationAiCoreComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }
    
    private void OnNewLink(Entity<StationAiCoreComponent> ent, ref NewLinkEvent args)
    {
        if (!TryComp<SiliconLawUpdaterComponent>(args.Sink, out var lawUpdater))
            return;

        if (ent.Comp.LawConsole != null)
            _deviceLinkSystem.RemoveSinkFromSource(ent.Comp.LawConsole.Value, ent.Owner); // Disconnect old console.

        ent.Comp.LawConsole = args.Sink;

        lawUpdater.Core = ent.Owner;
        Dirty(args.Sink, lawUpdater);
        Dirty(ent);
    }
    
    private void OnPortDisconnected(Entity<StationAiCoreComponent> ent, ref PortDisconnectedEvent args)
    {
        var lawConsoleEntityUid = ent.Comp.LawConsole;
        if (args.Port != ent.Comp.LinkingPort || lawConsoleEntityUid == null)
            return;

        if (TryComp<SiliconLawUpdaterComponent>(lawConsoleEntityUid, out var lawUpdater))
        {
            lawUpdater.Core = null;
            Dirty(lawConsoleEntityUid.Value, lawUpdater);
        }

        ent.Comp.LawConsole = null;
        Dirty(ent);
    }
}