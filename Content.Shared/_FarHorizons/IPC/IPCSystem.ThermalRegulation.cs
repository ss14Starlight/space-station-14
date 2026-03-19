using Content.Shared._FarHorizons.Silicons.IPC.Components;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;

namespace Content.Shared._FarHorizons.Silicons.IPC;

public abstract partial class SharedIPCSystem
{
    protected virtual void SetupThermals(){
        SubscribeLocalEvent<IPCThermalRegulationComponent, ExaminedEvent>(OnExamined);
    }

    protected abstract void UpdateThermals(float frameTime);

    private void OnExamined(Entity<IPCThermalRegulationComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushText(Loc.GetString(
                ent.Comp.FansCurrentlyOff || ent.Comp.CurrentMode == null ? 
                ent.Comp.FansOffExamineText :
                ent.Comp.CurrentMode.ExamineText, 
            ("entity", Identity.Entity(ent, EntityManager))), 10);
    }
}