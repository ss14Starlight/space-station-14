using Content.Shared._Starlight.Chemistry.Events;
using Content.Shared._Starlight.Implants.Components;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;

namespace Content.Shared._Starlight.Implants;

public sealed partial class InjectionPortSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnInjectorDoAfterTime(Entity<InjectionPortComponent> ent, ref InjectorMobDoAfterTimeEvent args) => args.DoAfterTime *= ent.Comp.InjectionSpeedMultiplier;

    [SubscribeLocalEvent]
    private void OnExamined(Entity<InjectionPortComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("injection-port-examine", ("target", Identity.Entity(ent, EntityManager))));
    }
}
