using System.Linq;
using Content.Client.Eui;
using Content.Shared.Administration.Notes;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.CustomControls;
using static Content.Shared.Administration.Notes.AdminNoteEuiMsg;

namespace Content.Client.Administration.UI.Notes;

[UsedImplicitly]
public sealed partial class AdminNotesEui : BaseEui // Starlight-edit (made partial)
{
    public AdminNotesEui()
    {
        NoteWindow = new AdminNotesWindow();
        NoteControl = NoteWindow.Notes;

        // Starlight-start
        NetworkNotesControl = NoteWindow.NetworkNotes;

        NoteWindow.NotesTabButton.OnPressed += _ => {
            NoteWindow.NotesTabButton.Disabled = true;
            NoteWindow.NetworkNotesTabButton.Disabled = false;
            NoteWindow.Notes.Visible = true;
            NoteWindow.NetworkNotes.Visible = false;
        };

        NoteWindow.NetworkNotesTabButton.OnPressed += _ => {
            NoteWindow.NotesTabButton.Disabled = false;
            NoteWindow.NetworkNotesTabButton.Disabled = true;
            NoteWindow.Notes.Visible = false;
            NoteWindow.NetworkNotes.Visible = true;
        };

        NoteControl.NoteChanged += (id, type, text, severity, secret, expiryTime, project) => SendMessage(new EditNoteRequest(id, type, text, severity, secret, expiryTime, false, project));
        NoteControl.NewNoteEntered += (type, text, severity, secret, expiryTime) => SendMessage(new CreateNoteRequest(type, text, severity, secret, expiryTime, false));
        NoteControl.NoteDeleted += (id, type, project) => SendMessage(new DeleteNoteRequest(id, type, project, false));
        NoteControl.SubWindowOpened += OnSubWindowOpened;

        NetworkNotesControl.NoteChanged += (id, type, text, severity, secret, expiryTime, project) => SendMessage(new EditNoteRequest(id, type, text, severity, secret, expiryTime, true, project));
        NetworkNotesControl.NewNoteEntered += (type, text, severity, secret, expiryTime) => SendMessage(new CreateNoteRequest(type, text, severity, secret, expiryTime, true));
        NetworkNotesControl.NoteDeleted += (id, type, project) => SendMessage(new DeleteNoteRequest(id, type, project, true));
        NetworkNotesControl.SubWindowOpened += OnSubWindowOpened;
        // Starlight-end

        // Starlight: removed OnClose in favour of sub in OnSubWindowOpened
    }

    public override void Closed()
    {
        base.Closed();

        // Starlight-start
        if (_openWindows.Count > 0)
        {
            var windows = _openWindows.ToArray();
            _openWindows.Clear();
            foreach (var window in windows)
                window.Close();
        }
        // Starlight-end
    }

    private AdminNotesWindow NoteWindow { get; }

    private AdminNotesControl NoteControl { get; }

    private AdminNotesControl NetworkNotesControl { get; } // Starlight-edit

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AdminNotesEuiState s)
        {
            return;
        }

        if (!NoteWindow.IsOpen) return; // Starlight

        NoteWindow.SetTitlePlayer(s.NotedPlayerName);
        NoteControl.SetPlayerName(s.NotedPlayerName);
        NoteControl.SetNotes(s.Notes);
        NoteControl.SetPermissions(s.CanCreate, s.CanDelete, s.CanEdit);

        // Starlight-start
        NetworkNotesControl.SetPlayerName(s.NotedPlayerName);
        NetworkNotesControl.SetNotes(s.NetworkNotes);
        NetworkNotesControl.SetPermissions(s.CanCreate, s.CanDelete, s.CanEdit);
        // Starlight-end
    }

    public override void Opened()
    {
        NoteWindow.OpenCentered();
        OnSubWindowOpened(NoteWindow); // Starlight
    }
}
