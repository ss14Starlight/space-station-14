using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Starlight.Objectives;

[RegisterComponent]
public sealed partial class PickerProgressTestComponent : Component
{
    public float Progress;
    public int Queries;
}

public sealed class PickerProgressTestSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PickerProgressTestComponent, ObjectiveGetProgressEvent>(OnProgress);
    }

    private void OnProgress(Entity<PickerProgressTestComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        ent.Comp.Queries++;
        args.Progress = ent.Comp.Progress;
    }
}
