using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server._Starlight.Mindshield;
using Content.Shared._Starlight.Mindshield.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles.Components; # Starlight-edit
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Mindshield;

/// <summary>
/// System used for adding or removing components with a mindshield implant
/// as well as checking if the implanted is a Rev or Head Rev.
/// </summary>
public sealed class MindShieldSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly MindshieldDegradationSystem _degradationSystem = default!; // STARLIGHT
    [Dependency] private readonly IGameTiming _timing = default!;  // STARLIGHT
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;  // STARLIGHT

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindShieldImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<MindShieldImplantComponent, EntGotRemovedFromContainerMessage>(OnImplantDraw);
    }

    private void OnImplantImplanted(Entity<MindShieldImplantComponent> ent, ref ImplantImplantedEvent ev)
    {
        if (ev.Implanted == null)
            return;

        EnsureComp<MindShieldComponent>(ev.Implanted.Value);
        MindShieldRemovalCheck(ev.Implanted.Value, ev.Implant);
    }

    /// <summary>
    /// Checks if the implanted person was a Rev or Head Rev and remove role or destroy mindshield respectively.
    /// </summary>
    private void MindShieldRemovalCheck(EntityUid implanted, EntityUid implant)
    {
        if (HasComp<HeadRevolutionaryComponent>(implanted))
        {
            // STARLIGHT: For head revolutionaries getting mindshielded, immediately destroy the mindshield
            // This is different from mindshielded personnel becoming head revolutionaries (which uses degradation)
            DestroyMindshieldImmediately(implanted);
            return;
        }

        if (_mindSystem.TryGetMind(implanted, out var mindId, out _) &&
            _roleSystem.MindRemoveRole<RevolutionaryRoleComponent>(mindId))
        {
            _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(implanted)} was deconverted due to being implanted with a Mindshield.");
        }
    }

    /// <summary>
    /// STARLIGHT: Immediately destroys a mindshield for head revolutionaries who get mindshielded.
    /// This is different from degradation - it's instant destruction.
    /// </summary>
    public void DestroyMindshieldImmediately(EntityUid uid)
    {
        // Remove the mindshield component
        if (HasComp<MindShieldComponent>(uid))
        {
            RemComp<MindShieldComponent>(uid);
            
            _adminLogManager.Add(LogType.Mind, LogImpact.Medium, 
                $"{ToPrettyString(uid)}'s mindshield was immediately destroyed as a head revolutionary.");
        }

        // Add the destroyed mindshield component to prevent re-implantation
        var destroyedComp = EnsureComp<DestroyedMindshieldComponent>(uid);
        destroyedComp.DestroyedAt = _timing.CurTime;
        Dirty(uid, destroyedComp);

        // Show destruction message to everyone around
        var name = Identity.Name(uid, EntityManager);
        var msg = Loc.GetString("head-rev-break-mindshield-name", ("target", name));
        _popupSystem.PopupEntity(msg, uid, PopupType.LargeCaution);

        // Play destruction sound in 5 tile radius
        _audioSystem.PlayPvs("/Audio/Effects/guardian_warn.ogg", uid);
    }

    // STARLIGHT END

    private void OnImplantDraw(Entity<MindShieldImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        RemComp<MindShieldComponent>(args.Container.Owner);
    }
}
