using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration.Notes;
using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Network;
using static Content.Shared.Administration.Notes.AdminNoteEuiMsg;

#region Starlight
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server._NullLink.Core;
using Content.Server._NullLink.EventBus;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.Players.PlayTimeTracking;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using AdminNote = Starlight.NullLink.AdminNote;
#endregion

namespace Content.Server.Administration.Notes;

public sealed class AdminNotesEui : BaseEui
{
    [Dependency] private readonly IAdminManager _admins = default!;
    [Dependency] private readonly IAdminNotesManager _notesMan = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    #region Starlight
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IActorRouter _actors = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly INullLinkEventBusManager _eventBus = default!;
    #endregion

    public AdminNotesEui()
    {
        IoCManager.InjectDependencies(this);
    }

    private Guid NotedPlayer { get; set; }
    private string NotedPlayerName { get; set; } = string.Empty;
    private bool HasConnectedBefore { get; set; }
    private Dictionary<(int, NoteType), SharedAdminNote> Notes { get; set; } = new();
    private Dictionary<(int, NoteType), SharedAdminNote> NetworkNotes { get; set; } = new(); // Starlight-edit

    public override async void Opened()
    {
        base.Opened();

        _admins.OnPermsChanged += OnPermsChanged;
        _notesMan.NoteAdded += NoteModified;
        _notesMan.NoteModified += NoteModified;
        _notesMan.NoteDeleted += NoteDeleted;
        _eventBus.NoteAdded += NoteModified;
        _eventBus.NoteChanged += NoteModified;
        _eventBus.NoteRemoved += NoteDeleted;
    }

    public override void Closed()
    {
        base.Closed();

        _admins.OnPermsChanged -= OnPermsChanged;
        _notesMan.NoteAdded -= NoteModified;
        _notesMan.NoteModified -= NoteModified;
        _notesMan.NoteDeleted -= NoteDeleted;
        // Starlight-start
        _eventBus.NoteAdded -= NoteModified;
        _eventBus.NoteChanged -= NoteModified;
        _eventBus.NoteRemoved -= NoteDeleted;
        // Starlight-end
    }

    public override EuiStateBase GetNewState()
    {
        return new AdminNotesEuiState(
            NotedPlayerName,
            Notes,
            _notesMan.CanCreate(Player) && HasConnectedBefore,
            _notesMan.CanDelete(Player),
            _notesMan.CanEdit(Player),
            NetworkNotes // Starlight-edit
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

                    var noteId = await _notesMan.AddAdminRemark(Player, NotedPlayer, request.NoteType, request.Message, request.NoteSeverity, request.Secret, request.ExpiryTime); // Starlight-edit

                    // Starlight-start
                    if (_actors.TryGetServerGrain(out var serverGrain) && noteId != null)
                        await serverGrain.AddOrUpdateNote(await GenerateNote(Player, NotedPlayer, request.NoteType, request.Message, request.NoteSeverity, request.Secret, request.ExpiryTime, noteId.Value));
                    // Starlight-end
                    break;
                }
            case DeleteNoteRequest request:
                {
                    if (!_notesMan.CanDelete(Player))
                    {
                        break;
                    }

                    // Starlight-start
                    if (request.Network)
                    {
                        if (_actors.TryGetServerGrain(out var serverGrain))
                            await serverGrain.RemoveNote(NotedPlayer, request.Id, request.Project, Player.UserId);
                        break;
                    }
                    else
                    {
                        if (_actors.TryGetServerGrain(out var serverGrain))
                            await serverGrain.RemoveNote(NotedPlayer, request.Id, removedBy: Player.UserId);
                    }
                    // Starlight-end

                    await _notesMan.DeleteAdminRemark(request.Id, request.Type, Player, null); // Starlight-edit
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


                    var note = await _notesMan.ModifyAdminRemark(request.Id, request.Type, Player, request.Message, request.NoteSeverity, request.Secret, request.ExpiryTime, null, null); // Starlight-edit

                    // Starlight-start
                    if (note != null && _actors.TryGetServerGrain(out var serverGrain))
                    {
                        var newNote = new AdminNote() {
                            Id = note.Id,
                            Player = note.Player,
                            ProjectName = note.ProjectName,
                            ServerName = note.ServerName ?? _actors.Server ?? "Unknown",
                            Round = note.Round,
                            PlaytimeAtNote = note.PlaytimeAtNote,
                            NoteType = note.NoteType.ToString(),
                            Message = note.Message,
                            NoteSeverity = note.NoteSeverity.ToString(),
                            Secret = note.Secret,
                            CreatedByName = note.CreatedByName,
                            EditedByName = note.EditedByName,
                            CreatedAt = note.CreatedAt,
                            LastEditedAt = note.LastEditedAt,
                            ExpiryTime = note.ExpiryTime,
                            BannedRoles = note.BannedRoles,
                            UnbannedTime = note.UnbannedTime,
                            UnbannedByName = note.UnbannedByName,
                            Seen = note.Seen,
                            EditedBy = Player.UserId,
                            RemovedBy = null
                        };
                        if (request.Network)
                        {
                            await serverGrain.AddOrUpdateNote(newNote, request.Project);
                            break;
                        }
                        else
                            await serverGrain.AddOrUpdateNote(newNote);
                    }
                    // Starlight-end

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

    #region Starlight

    private async void NoteModified(AdminNote note)
    {
        if (note.Player != NotedPlayer || !TryConvert(note, out var converted))
            return;

        NetworkNotes[(converted.Id, converted.NoteType)] = converted;
        if (converted.ProjectName == _actors.Project && converted.ServerName == _actors.Server)
        {
            NoteModified(converted);
            return;
        }

        StateDirty();
    }

    private void NoteDeleted(AdminNote note)
    {
        if (note.Player != NotedPlayer || !Enum.TryParse<NoteType>(note.NoteType, true, out var type))
            return;

        NetworkNotes.Remove((note.Id, type));
        if (note.ProjectName == _actors.Project && note.ServerName == _actors.Server)
        {
            Notes.Remove((note.Id, type));
        }

        StateDirty();
    }

    private Dictionary<(int, NoteType), SharedAdminNote> Convert(IEnumerable<AdminNote> notes)
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

    private async Task<AdminNote> GenerateNote(ICommonSession createdBy, Guid player, NoteType type, string message, NoteSeverity? severity, bool secret, DateTime? expiryTime, int noteId)
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

        var gameTicker = _entities.System<GameTicker>();
        int? roundId = gameTicker.RoundId == 0 ? null : gameTicker.RoundId;
        var serverName = _config.GetCVar(CCVars.AdminLogsServerName); // This could probably be done another way, but this is fine. For displaying only.
        var createdAt = DateTime.UtcNow;
        var playtime = (await _db.GetPlayTimes(player)).Find(p => p.Tracker == PlayTimeTrackingShared.TrackerOverall)?.TimeSpent ?? TimeSpan.Zero;
        bool? seen = null;

        switch (type)
        {
            case NoteType.Watchlist:
                secret = true;
                break;
            case NoteType.Message:
                seen = false;
                break;
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

    #endregion
}
