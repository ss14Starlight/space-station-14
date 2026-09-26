using System.Globalization;
using Content.Server.Chat.Managers;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;

namespace Content.Server.Roles.Jobs;

/// <summary>
///     Handles the job data on mind entities.
/// </summary>
public sealed partial class JobSystem : SharedJobSystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private RoleSystem _roles = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAddedEvent);
        SubscribeLocalEvent<RoleRemovedEvent>(OnRoleRemovedEvent);
    }

    private void OnRoleAddedEvent(RoleAddedEvent args)
    {
        if (!args.Silent) MindOnDoGreeting(args.MindId, args.Mind);

        if (args.RoleTypeUpdate)
            _roles.RoleUpdateMessage(args.Mind);
    }

    private void OnRoleRemovedEvent(RoleRemovedEvent args)
    {
        if (args.RoleTypeUpdate)
            _roles.RoleUpdateMessage(args.Mind);
    }

    // Starlght edit: remove need for RoleAddedEvent to be passed + make public
    public void MindOnDoGreeting(EntityUid mindId, MindComponent component, bool skipRole = false)
    {
    // Starlight end
        if (!_player.TryGetSessionById(component.UserId, out var session))
            return;

        if (!MindTryGetJob(mindId, out var prototype))
            return;

        if (!skipRole) _chat.DispatchServerMessage(session, Loc.GetString("job-greet-introduce-job-name", // Starlight edit
            ("jobName", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prototype.LocalizedName))));

        if (prototype.RequireAdminNotify)
            _chat.DispatchServerMessage(session, Loc.GetString("job-greet-important-disconnect-admin-notify"));

        _chat.DispatchServerMessage(session, Loc.GetString("job-greet-supervisors-warning", ("jobName", prototype.LocalizedName), ("supervisors", Loc.GetString(prototype.Supervisors))));

        // Starlight start
        if (prototype.JobRules is not null)
            _chat.DispatchServerMessage(session, Loc.GetString("job-greet-information-rules", ("jobRules", Loc.GetString(prototype.JobRules))));
        // Starlight end
    }

    public void MindAddJob(EntityUid mindId, string jobPrototypeId)
    {
        if (MindHasJobWithId(mindId, jobPrototypeId))
            return;

        _roles.MindAddJobRole(mindId, null, false, jobPrototypeId);
    }
}
