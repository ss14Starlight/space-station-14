using Content.Client.Eui;
using Content.Shared.Administration.Notes;
using Content.Shared.Eui;
using JetBrains.Annotations;
using static Content.Shared.Administration.Notes.AdminNoteEuiMsg;

namespace Content.Client.Administration.UI.Notes;

[UsedImplicitly]
public sealed class AdminNotesEui : BaseEui
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

        NetworkNotesControl.NoteChanged += (id, type, text, severity, secret, expiryTime, project) => SendMessage(new EditNoteRequest(id, type, text, severity, secret, expiryTime, true, project));
        NetworkNotesControl.NewNoteEntered += (type, text, severity, secret, expiryTime) => SendMessage(new CreateNoteRequest(type, text, severity, secret, expiryTime, true));
        NetworkNotesControl.NoteDeleted += (id, type, project) => SendMessage(new DeleteNoteRequest(id, type, project, true));
        // Starlight-end

        NoteWindow.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Closed()
    {
        base.Closed();
        NoteWindow.Close();
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
    }
}
