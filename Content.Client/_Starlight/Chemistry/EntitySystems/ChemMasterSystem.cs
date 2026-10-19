using Content.Client._Starlight.Chemistry.UI;
using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DragDrop;
using Content.Shared.Plumbing.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Containers;

// ReSharper disable CheckNamespace
namespace Content.Client.Chemistry.EntitySystems;

/// <summary>
/// Contains all the client-side logic for ChemMasters.
/// <seealso cref="ReagentDispenserComponent"/>
/// </summary>
[UsedImplicitly]
public sealed partial class ChemMasterSystem : SharedChemMasterSystem
{
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeUpdateUi<ComponentStartup>(ChemMasterUIDirtyFlags.All);
        SubscribeUpdateUi<SolutionChangedEvent>(ChemMasterUIDirtyFlags.Solutions);
        SubscribeUpdateUi<EntInsertedIntoContainerMessage>(ChemMasterUIDirtyFlags.Inventory);
        SubscribeUpdateUi<EntRemovedFromContainerMessage>(ChemMasterUIDirtyFlags.Inventory);
        SubscribeUpdateUi<BoundUIOpenedEvent>(ChemMasterUIDirtyFlags.All);
        SubscribeUpdateUi<DragDropTargetEvent>(ChemMasterUIDirtyFlags.All); // TRIESTE
        SubscribeUpdateUi<AfterAutoHandleStateEvent>(ChemMasterUIDirtyFlags.Settings);
        SubscribeUpdateUi<PlumbingOutputAutoStateEvent>(ChemMasterUIDirtyFlags.PlumbingOutput);
    }

    private void SubscribeUpdateUi<TEvent>(ChemMasterUIDirtyFlags flags) where TEvent : notnull
    {
        void ChemMasterUiUpdateHandler(Entity<ChemMasterComponent> ent, ref TEvent ev) => UpdateUi(ent, flags);

        SubscribeLocalEvent<ChemMasterComponent, TEvent>(ChemMasterUiUpdateHandler);
    }

    protected override void UpdateUi(Entity<ChemMasterComponent> ent, ChemMasterUIDirtyFlags flags)
    {
        if (_userInterfaceSystem.TryGetOpenUi(ent.Owner, ChemMasterUiKey.Key, out ModernChemMasterBui? bui))
            bui.Update(flags);
    }
}
