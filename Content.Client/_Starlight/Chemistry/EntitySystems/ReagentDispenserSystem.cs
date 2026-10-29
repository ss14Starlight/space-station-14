using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Plumbing.EntitySystems;
using Content.Shared.Power;
using Content.Shared.Storage.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Containers;

// ReSharper disable CheckNamespace
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

        SubscribeLocalEvent<ReagentDispenserComponent, ComponentStartup>(SubscribeUpdateUi);
        SubscribeLocalEvent<ReagentDispenserComponent, SolutionChangedEvent>(SubscribeUpdateUi);
        SubscribeLocalEvent<ReagentDispenserComponent, EntInsertedIntoContainerMessage>(SubscribeUpdateUi, after: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<ReagentDispenserComponent, EntRemovedFromContainerMessage>(SubscribeUpdateUi, after: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<ReagentDispenserComponent, PowerChangedEvent>(SubscribeUpdateUi);
        SubscribeLocalEvent<ReagentDispenserComponent, AfterAutoHandleStateEvent>(SubscribeUpdateUi);
        SubscribeLocalEvent<ReagentDispenserComponent, PlumbingOutputAutoStateEvent>(SubscribeUpdateUi);
    }

    private void SubscribeUpdateUi<T>(Entity<ReagentDispenserComponent> ent, ref T ev) => UpdateUi(ent);

    protected override void UpdateUi(Entity<ReagentDispenserComponent> entity)
    {
        if (_userInterfaceSystem.TryGetOpenUi(entity.Owner, ReagentDispenserUiKey.Key, out var bui))
            bui.Update();
    }
}
