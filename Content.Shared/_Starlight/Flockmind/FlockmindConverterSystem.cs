using Content.Shared.Actions;
using Content.Shared.DoAfter;

namespace Content.Shared._Starlight.Flockmind;

public sealed partial class FlockmindConverterSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlockmindConverterComponent, FlockmindConvertTileEvent>(OnTileConvert);
    }

    private void OnTileConvert(Entity<FlockmindConverterComponent> ent, ref FlockmindConvertTileEvent ev)
    {
        Log.Debug("TODO!!! IMPLEMENT THIS!!!");
        //todo: implement this
    }
}

public sealed partial class FlockmindConvertTileEvent : WorldTargetActionEvent
{
    [DataField]
    public int ConvertCost = 15;
}
