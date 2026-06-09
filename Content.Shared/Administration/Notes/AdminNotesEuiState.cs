using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Notes;

[Serializable, NetSerializable]
public sealed class AdminNotesEuiState : EuiStateBase
{
    public AdminNotesEuiState(string notedPlayerName, Dictionary<(int, NoteType), SharedAdminNote> notes, bool canCreate, bool canDelete, bool canEdit, Dictionary<(int, NoteType, string, string), SharedAdminNote> networkNotes) // Starlight-edit: network notes
    {
        NotedPlayerName = notedPlayerName;
        Notes = notes;
        NetworkNotes = networkNotes; // Starlight-edit: network notes
        CanCreate = canCreate;
        CanDelete = canDelete;
        CanEdit = canEdit;
    }

    public string NotedPlayerName { get; }
    public Dictionary<(int noteId, NoteType noteType), SharedAdminNote> Notes { get; }
    public Dictionary<(int noteId, NoteType noteType, string, string), SharedAdminNote> NetworkNotes { get; } // Starlight-edit: network notes
    public bool CanCreate { get; }
    public bool CanDelete { get; }
    public bool CanEdit { get; }
}

public static class AdminNoteEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class CreateNoteRequest : EuiMessageBase
    {
        public CreateNoteRequest(NoteType type, string message, NoteSeverity? severity, bool secret, DateTime? expiryTime, bool network) // Starlight-edit: network notes
        {
            NoteType = type;
            Message = message;
            NoteSeverity = severity;
            Secret = secret;
            ExpiryTime = expiryTime;
            Network = network; // Starlight-edit: network notes
        }

        public NoteType NoteType { get; set; }
        public string Message { get; set; }
        public NoteSeverity? NoteSeverity { get; set; }
        public bool Secret { get; set; }
        public DateTime? ExpiryTime { get; set; }
        public bool Network { get; set; } // Starlight-edit: network notes
    }

    [Serializable, NetSerializable]
    public sealed class DeleteNoteRequest : EuiMessageBase
    {
        public DeleteNoteRequest(int id, NoteType type, string? project, bool network) // Starlight-edit: network notes
        {
            Id = id;
            Type = type;
            Network = network; // Starlight-edit: network notes
            Project = project; // Starlight-edit: network notes
        }

        public int Id { get; set; }
        public NoteType Type { get; set; }
        public string? Project { get; set; }
        public bool Network { get; set; }
    }

    [Serializable, NetSerializable]
    public sealed class EditNoteRequest : EuiMessageBase
    {
        public EditNoteRequest(int id, NoteType type, string message, NoteSeverity? severity, bool secret, DateTime? expiryTime, bool network, string? project) // Starlight-edit: network notes
        {
            Id = id;
            Type = type;
            Message = message;
            NoteSeverity = severity;
            Secret = secret;
            ExpiryTime = expiryTime;
            Network = network; // Starlight-edit: network notes
            Project = project; // Starlight-edit: network notes
        }

        public int Id { get; set; }
        public NoteType Type { get; set; }
        public string Message { get; set; }
        public NoteSeverity? NoteSeverity { get; set; }
        public bool Secret { get; set; }
        public DateTime? ExpiryTime { get; set; }
        public string? Project { get; set; } // Starlight-edit: network notes
        public bool Network { get; set; } // Starlight-edit: network notes
    }
}
