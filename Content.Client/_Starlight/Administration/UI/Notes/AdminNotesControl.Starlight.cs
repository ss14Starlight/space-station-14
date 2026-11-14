using System.Linq;
using Content.Shared.Administration.Notes;
using Content.Shared.Database;
using Robust.Client.UserInterface.CustomControls;

// ReSharper disable CheckNamespace
namespace Content.Client.Administration.UI.Notes;

partial class AdminNotesControl
{
    public event Action<BaseWindow>? SubWindowOpened;

    public void SetNotes(Dictionary<(int, NoteType, string, string), SharedAdminNote> notes)
    {
        foreach (var (key, input) in NetworkInputs)
        {
            if (!notes.ContainsKey(key))
            {
                // Yes this is slower than just updating, but new notes get added at the bottom. The user won't notice.
                Notes.RemoveAllChildren();
                NetworkInputs.Clear();
                break;
            }
            Notes.RemoveChild(input);
            NetworkInputs.Remove(key);
        }

        var showMoreButtonVisible = false;
        foreach (var note in notes.Values.OrderByDescending(note => note.CreatedAt))
        {
            if (NetworkInputs.TryGetValue((note.Id, note.NoteType, note.ServerName ?? "", note.ProjectName ?? ""), out var input))
            {
                input.UpdateNote(note);
                continue;
            }

            input = new AdminNotesLine(_sprites, note);
            input.OnClicked += NoteClicked;
            input.OnMouseEntered += NoteMouseEntered;
            input.OnMouseExited += NoteMouseExited;

            UpdateNoteLineAlpha(input);

            if (input.Modulate.A == 0)
            {
                input.Visible = false;
                showMoreButtonVisible = true;
            }

            Notes.AddChild(input);
            NetworkInputs[(note.Id, note.NoteType, note.ServerName ?? "", note.ProjectName ?? "")] = input;
            ShowMoreButton.Visible = showMoreButtonVisible;
        }
    }
}
