using Content.Client._Starlight.Chemistry.UI;
using Content.Server.Chemistry.Components;
using Content.Shared._Starlight.Plumbing.Components;
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
/// Contains all the client-side logic for reagent dispensers.
/// <seealso cref="ReagentDispenserComponent"/>
/// </summary>
[UsedImplicitly]
public sealed partial class ChemMasterSystem : SharedChemMasterSystem
{
    [Dependency] private SharedUserInterfaceSystem _userInterfaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemMasterComponent, ComponentStartup>(SubscribeUpdateUiState);
        SubscribeLocalEvent<ChemMasterComponent, SolutionChangedEvent>(SubscribeUpdateUiState);
        SubscribeLocalEvent<ChemMasterComponent, EntInsertedIntoContainerMessage>(SubscribeUpdateUiState);
        SubscribeLocalEvent<ChemMasterComponent, EntRemovedFromContainerMessage>(SubscribeUpdateUiState);
        SubscribeLocalEvent<ChemMasterComponent, BoundUIOpenedEvent>(SubscribeUpdateUiState);
        SubscribeLocalEvent<ChemMasterComponent, DragDropTargetEvent>(SubscribeUpdateUiState); // TRIESTE
        SubscribeLocalEvent<ChemMasterComponent, AfterAutoHandleStateEvent>(SubscribeUpdateUiState);
        SubscribeLocalEvent<ChemMasterComponent, PlumbingOutputChangedEvent>(SubscribeUpdateUiState);
    }

    private void SubscribeUpdateUiState<T>(Entity<ChemMasterComponent> ent, ref T ev) => UpdateUi(ent);

    protected override void UpdateUi(Entity<ChemMasterComponent> ent, bool updateLabel = false)
    {
        if (_userInterfaceSystem.TryGetOpenUi(ent.Owner, ChemMasterUiKey.Key, out ModernChemMasterBui? bui))
            bui.Update(updateLabel);
    }
}
