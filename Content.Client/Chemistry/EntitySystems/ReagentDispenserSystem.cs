using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client.Chemistry.EntitySystems;

/// <summary>
/// Contains all the client-side logic for reagent dispensers.
/// <seealso cref="ReagentDispenserComponent"/>
/// </summary>
[UsedImplicitly]
public sealed partial class ReagentDispenserSystem : SharedReagentDispenserSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReagentDispenserComponent, AfterAutoHandleStateEvent>(OnAutoState);
    }

    private void OnAutoState(Entity<ReagentDispenserComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateUi(ent);

    protected override void UpdateUi(Entity<ReagentDispenserComponent> entity)
    {
        if (_userInterfaceSystem.TryGetOpenUi(entity.Owner, ReagentDispenserUiKey.Key, out var bui))
            bui.Update();
    }
}
