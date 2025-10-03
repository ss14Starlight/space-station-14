using Content.Shared._Starlight.Devil;
using Content.Shared._Starlight.Devil.Damnations;

namespace Content.Server._Starlight.Devil.Damnations;

public sealed partial class TestFailDamnationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TestFailDamnationComponent, MapInitEvent>(OnInit);
    }

    public void OnInit(Entity<TestFailDamnationComponent> ent, ref MapInitEvent args)
    {
        var ev = new DamnationInitFailEvent();
        RaiseLocalEvent(ent, ref ev);
    }
}