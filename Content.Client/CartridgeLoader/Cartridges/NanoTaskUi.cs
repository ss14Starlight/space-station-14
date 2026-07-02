using System.Linq;
using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.CartridgeLoader.Cartridges;

/// <summary>
///     UI fragment responsible for displaying Tidr controls in a PDA and coordinating with the NanoTaskCartridgeSystem for state
/// </summary>
public sealed partial class NanoTaskUi : UIFragment
{
    private NanoTaskUiFragment? _fragment;
    private NanoTaskItemPopup? _popup;
    private NanoTaskDetailsPopup? _details; // Starlight - Tidr: read-only view for non-owners

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new NanoTaskUiFragment();
        _popup = new NanoTaskItemPopup();
        _details = new NanoTaskDetailsPopup();
        _fragment.NewTask += () =>
        {
            _popup.ResetInputs(null);
            _popup.SetEditingTaskId(null);
            _popup.OpenCentered();
        };
        // Starlight - Tidr: owners get the editable form; everyone else gets read-only details
        _fragment.OpenTask += id =>
        {
            if (_fragment.Tasks.Find(e => e.Task.Id == id) is not NanoTaskViewerEntry entry)
                return;

            if (entry.ViewerIsOwner)
            {
                _popup.ResetInputs(entry.Task.Data);
                _popup.SetEditingTaskId(entry.Task.Id);
                _popup.OpenCentered();
            }
            else
            {
                _details.SetTask(entry.Task.Data);
                _details.OpenCentered();
            }
        };
        // Starlight - Tidr: Complete only (completion is final; there is no un-complete)
        _fragment.ToggleTaskCompletion += id =>
        {
            if (_fragment.Tasks.Find(e => e.Task.Id == id) is not NanoTaskViewerEntry entry)
                return;
            var data = entry.Task.Data;
            if (data.IsTaskDone)
                return;

            userInterface.SendMessage(new CartridgeUiMessage(new NanoTaskUiMessageEvent(new NanoTaskUpdateTask(new(id, new(
                description: data.Description,
                taskIsFor: data.TaskIsFor,
                isTaskDone: true,
                priority: data.Priority,
                location: data.Location,
                reward: data.Reward,
                acceptedBy: data.AcceptedBy
            ))))));
        };
        // Starlight - Tidr: claim or release a job (server decides which based on the inserted card)
        _fragment.AcceptTask += id =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new NanoTaskUiMessageEvent(new NanoTaskAcceptTask(id))));
        };
        _popup.TaskSaved += (id, data) =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new NanoTaskUiMessageEvent(new NanoTaskUpdateTask(new(id, data)))));
            _popup.Close();
        };
        _popup.TaskDeleted += id =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new NanoTaskUiMessageEvent(new NanoTaskDeleteTask(id))));
            _popup.Close();
        };
        _popup.TaskCreated += data =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new NanoTaskUiMessageEvent(new NanoTaskAddTask(data))));
            _popup.Close();
        };
        _popup.TaskPrinted += data =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new NanoTaskUiMessageEvent(new NanoTaskPrintTask(data))));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not NanoTaskUiState nanoTaskState)
            return;

        _fragment?.UpdateState(nanoTaskState.Tasks, nanoTaskState.ViewerBalance);
    }
}
