using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared._Starlight.Mindshield;
using Content.Shared._Starlight.Mindshield.Components;
using Content.Shared.Audio;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Revolutionary.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Starlight.Mindshield;

/// <summary>
/// Server-side system for handling mindshield degradation for head revolutionaries.
/// </summary>
public sealed class MindshieldDegradationSystem : SharedMindshieldDegradationSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    private readonly List<EntityUid> _toUpdate = new();

    public override void Initialize()
    {
        base.Initialize();
        
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = Timing.CurTime;
        var enumerator = EntityQueryEnumerator<MindshieldDegradationComponent>();
        _toUpdate.Clear();

        while (enumerator.MoveNext(out var uid, out var degradation))
        {
            // Skip if already complete
            if (degradation.DegradationComplete)
                continue;

            // Check if warning should be shown
            if (ShouldShowWarning(uid, degradation))
            {
                ShowDegradationWarning(uid, degradation);
                degradation.WarningShown = true;
                Dirty(uid, degradation);
            }

            // Check if degradation is complete
            if (IsDegradationComplete(uid, degradation))
            {
                _toUpdate.Add(uid);
            }
        }

        // Process completed degradations
        foreach (var uid in _toUpdate)
        {
            CompleteMindshieldDegradation(uid);
        }
    }

    /// <summary>
    /// Shows the warning message when mindshield starts degrading (at 5 minutes)
    /// </summary>
    private void ShowDegradationWarning(EntityUid uid, MindshieldDegradationComponent degradation)
    {
        _popupSystem.PopupEntity(Loc.GetString("mindshield-degrading-warning"), uid, uid);
        
        _adminLogManager.Add(LogType.Mind, LogImpact.Medium, 
            $"{ToPrettyString(uid)} received mindshield degradation warning as a head revolutionary.");
    }

    /// <summary>
    /// Completes the mindshield degradation process (at 10 minutes)
    /// </summary>
    private void CompleteMindshieldDegradation(EntityUid uid)
    {
        if (!TryComp<MindshieldDegradationComponent>(uid, out var degradation))
            return;

        // Mark as complete
        degradation.DegradationComplete = true;
        Dirty(uid, degradation);

        // Show destruction message to everyone around
        var name = Identity.Name(uid, EntityManager);
        var msg = Loc.GetString("mindshield-destroyed-name", ("target", name));
        _popupSystem.PopupEntity(msg, uid, PopupType.LargeCaution);

        // Play destruction sound in 5 tile radius
        _audioSystem.PlayPvs("/Audio/Effects/guardian_warn.ogg", uid);

        // Remove the actual mindshield component (this will make the icon disappear for everyone)
        if (HasComp<MindShieldComponent>(uid))
        {
            RemComp<MindShieldComponent>(uid);
            
            _adminLogManager.Add(LogType.Mind, LogImpact.Medium, 
                $"{ToPrettyString(uid)}'s mindshield was destroyed due to degradation as a head revolutionary.");
        }

        // Add the destroyed mindshield component to prevent re-implantation
        var destroyedComp = EnsureComp<DestroyedMindshieldComponent>(uid);
        destroyedComp.DestroyedAt = Timing.CurTime;
        Dirty(uid, destroyedComp);

        // Remove the degradation component (this will also clean up the status effect)
        RemComp<MindshieldDegradationComponent>(uid);
    }

    /// <summary>
    /// Starts the mindshield degradation process for a head revolutionary
    /// </summary>
    public override void StartMindshieldDegradation(EntityUid uid)
    {
        base.StartMindshieldDegradation(uid);
        
        _adminLogManager.Add(LogType.Mind, LogImpact.Medium, 
            $"{ToPrettyString(uid)} started mindshield degradation as a head revolutionary.");
    }
}
