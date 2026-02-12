
using Content.Shared.Examine;

namespace Content.Shared._Starlight.UXN;
public abstract partial class SharedUxnSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UxnComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<UxnAttachableComponent, ExaminedEvent>(OnExamineAttachable);
    }

    private void OnExamined(Entity<UxnComponent> ent, ref ExaminedEvent args) => args.PushMarkup(Loc.GetString("uxn-component-examine", [("compilerOutput", ent.Comp.CompilerOutput),
            ("assembledSize", ent.Comp.AssembledSize)]));

    private void OnExamineAttachable(Entity<UxnAttachableComponent> ent, ref ExaminedEvent args)
    {
        //if (args.Examiner) todo: find examples. I want it to only show this if you hold a UXN chip.
        args.PushMarkup(Loc.GetString("uxn-attachable-component-examine"));
    }
}