using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server._NullLink.Core;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared.Administration.Notes;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Eui;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using static Content.Shared.Administration.Notes.AdminNoteEuiMsg;
using AdminNote = Starlight.NullLink.AdminNote;

namespace Content.Server.Administration.Notes;

public sealed class AdminNotesEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IAdminNotesManager _notesMan = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IActorRouter _actors = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    public AdminNotesEui()
    {
        IoCManager.InjectDependencies(this);
    }

    private Guid NotedPlayer { get; set; }
    private string NotedPlayerName { get; set; } = string.Empty;
    private bool HasConnectedBefore { get; set; }
    private Dictionary<(int, NoteType), SharedAdminNote> Notes { get; set; } = new();
    private Dictionary<(int, NoteType), SharedAdminNote> NetworkNotes { get; set; } = new();

    public override async void Opened()
    {
        base.Opened();

        _admins.OnPermsChanged += OnPermsChanged;
        _notesMan.NoteAdded += NoteModified;
        _notesMan.NoteModified += NoteModified;
        _notesMan.NoteDeleted += NoteDeleted;
    }

    public override void Closed()
    {
        base.Closed();

        _admins.OnPermsChanged -= OnPermsChanged;
        _notesMan.NoteAdded -= NoteModified;
        _notesMan.NoteModified -= NoteModified;
        _notesMan.NoteDeleted -= NoteDeleted;
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminNotesEuiState(
            NotedPlayerName,
            Notes,
            _notesMan.CanCreate(Player) && HasConnectedBefore,
            _notesMan.CanDelete(Player),
            _notesMan.CanEdit(Player),
            NetworkNotes
        );
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        switch (msg)
        {
            case CreateNoteRequest request:
                {
                    if (!_notesMan.CanCreate(Player))
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(request.Message))
                    {
                        break;
                    }

                    if (request.ExpiryTime is not null && request.ExpiryTime <= DateTime.UtcNow)
                    {
                        break;
                    }

                    await _notesMan.AddAdminRemark(Player, NotedPlayer, request.NoteType, request.Message, request.NoteSeverity, request.Secret, request.ExpiryTime);
                    break;
                }
            case DeleteNoteRequest request:
                {
                    if (!_notesMan.CanDelete(Player))
                    {
                        break;
                    }

                    if (request.Network)
                    {
                        if (_actors.TryGetServerGrain(out var serverGrain))
                            await serverGrain.RemoveNote(NotedPlayer, request.Id, request.Project);
                        break;
                    }

                    await _notesMan.DeleteAdminRemark(request.Id, request.Type, Player);
                    break;
                }
            case EditNoteRequest request:
                {
                    if (!_notesMan.CanEdit(Player))
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(request.Message))
                    {
                        break;
                    }

                    if (request.Network)
                    {
                        if (_actors.TryGetServerGrain(out var serverGrain))
                            await serverGrain.AddOrUpdateNote(NotedPlayer, await GenerateNote(Player, NotedPlayer, request.Type, request.Message, request.NoteSeverity, request.Secret, request.ExpiryTime), request.Project);
                        break;
                    }

                    await _notesMan.ModifyAdminRemark(request.Id, request.Type, Player, request.Message, request.NoteSeverity, request.Secret, request.ExpiryTime);
                    break;
                }
        }
    }

    public async Task ChangeNotedPlayer(Guid notedPlayer)
    {
        NotedPlayer = notedPlayer;
        await LoadFromDb();
    }

    private void NoteModified(SharedAdminNote note)
    {
        if (note.Player != NotedPlayer)
            return;

        Notes[(note.Id, note.NoteType)] = note;
        StateDirty();
    }

    private void NoteDeleted(SharedAdminNote note)
    {
        if (note.Player != NotedPlayer)
            return;

        Notes.Remove((note.Id, note.NoteType));
        StateDirty();
    }

    private async Task LoadFromDb()
    {
        var locatedPlayer = await _locator.LookupIdAsync((NetUserId) NotedPlayer);
        NotedPlayerName = locatedPlayer?.Username ?? string.Empty;
        HasConnectedBefore = locatedPlayer?.LastAddress is not null;
        Notes = (from note in await _notesMan.GetAllAdminRemarks(NotedPlayer)
                 select note.ToShared())
            .ToDictionary(sharedNote => (sharedNote.Id, sharedNote.NoteType));
        if (_actors.TryGetServerGrain(out var serverGrain))
            NetworkNotes = Convert(await serverGrain.RequestNotes(NotedPlayer) ?? []);
        StateDirty();
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player != Player)
        {
            return;
        }

        if (!_notesMan.CanView(Player))
        {
            Close();
        }
        else
        {
            StateDirty();
        }
    }

    private Dictionary<(int, NoteType), SharedAdminNote> Convert(HashSet<AdminNote> notes)
    {
        Dictionary<(int, NoteType), SharedAdminNote> pairs = [];

        foreach (var note in notes)
        {
            if (!TryConvert(note, out var newNote))
                continue;

            pairs.Add((newNote.Id, newNote.NoteType), newNote);
        }

        return pairs;
    }

    private bool TryConvert(AdminNote note, [NotNullWhen(true)] out SharedAdminNote? converted)
    {
        converted = null;
        if (!Enum.TryParse<NoteType>(note.NoteType, true, out var type) || !Enum.TryParse<NoteSeverity>(note.NoteSeverity, true, out var severity))
            return false;

        converted = new SharedAdminNote(note.Id, new NetUserId(note.Player), note.Round, note.ServerName, note.ProjectName, note.PlaytimeAtNote, type, note.Message, severity, note.Secret, note.CreatedByName, note.EditedByName, note.CreatedAt, note.LastEditedAt, note.ExpiryTime, note.BannedRoles, note.UnbannedTime, note.UnbannedByName, note.Seen, true);

        return true;
    }

    private async Task<AdminNote> GenerateNote(ICommonSession createdBy, Guid player, NoteType type, string message, NoteSeverity? severity, bool secret, DateTime? expiryTime)
    {
        message = message.Trim();

        var sb = new StringBuilder($"{createdBy.Name} added a");

        if (secret && type == NoteType.Note)
        {
            sb.Append(" secret");
        }

        switch (type)
        {
            case NoteType.Note:
                sb.Append($" with {severity} severity");
                break;
            case NoteType.Message:
                severity = null;
                secret = false;
                break;
            case NoteType.Watchlist:
                severity = null;
                secret = true;
                break;
            case NoteType.ServerBan:
            case NoteType.RoleBan:
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown note type");
        }

        if (expiryTime is not null)
        {
            sb.Append($" which expires on {expiryTime.Value.ToUniversalTime(): yyyy-MM-dd HH:mm:ss} UTC");
        }

        int? roundId = _gameTicker.RoundId == 0 ? null : _gameTicker.RoundId;
        var serverName = _config.GetCVar(CCVars.AdminLogsServerName); // This could probably be done another way, but this is fine. For displaying only.
        var createdAt = DateTime.UtcNow;
        var playtime = (await _db.GetPlayTimes(player)).Find(p => p.Tracker == PlayTimeTrackingShared.TrackerOverall)?.TimeSpent ?? TimeSpan.Zero;
        int noteId;
        bool? seen = null;

        switch (type)
        {
            case NoteType.Note:
                if (severity is null)
                    throw new ArgumentException("Severity cannot be null for a note", nameof(severity));
                noteId = await _db.AddAdminNote(roundId, player, playtime, message, severity.Value, secret, createdBy.UserId, createdAt, expiryTime);
                break;
            case NoteType.Watchlist:
                secret = true;
                noteId = await _db.AddAdminWatchlist(roundId, player, playtime, message, createdBy.UserId, createdAt, expiryTime);
                break;
            case NoteType.Message:
                noteId = await _db.AddAdminMessage(roundId, player, playtime, message, createdBy.UserId, createdAt, expiryTime);
                seen = false;
                break;
            case NoteType.ServerBan: // Add bans using the ban panel, not note edit
            case NoteType.RoleBan:
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown note type");
        }

        var note = new AdminNote() {
            Id =  noteId,
            Player = player,
            Round = roundId,
            ServerName = serverName,
            ProjectName = null,
            PlaytimeAtNote = playtime,
            NoteType = type.ToString(),
            Message = message,
            NoteSeverity = severity.ToString(),
            Secret = secret,
            CreatedByName = createdBy.Name,
            CreatedAt = createdAt,
            ExpiryTime = expiryTime,
            BannedRoles = null,
            UnbannedTime = null,
            UnbannedByName = null,
            LastEditedAt = null,
            Seen = seen,
        };

        return note;
    }
}
