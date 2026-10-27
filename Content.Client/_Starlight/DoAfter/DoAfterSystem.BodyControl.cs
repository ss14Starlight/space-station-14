// ReSharper disable CheckNamespace
namespace Content.Client.DoAfter;

public sealed partial class DoAfterSystem
{
    protected override bool ShouldUpdateDoAfter(Shared.DoAfter.DoAfter doAfter)
        => doAfter.Args.HandController is not { } controller || controller == GetNetEntity(_player.LocalEntity);
}
