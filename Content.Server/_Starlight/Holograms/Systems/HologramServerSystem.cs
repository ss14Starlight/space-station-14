using Content.Shared.Popups;
using Content.Shared._Starlight.Holograms;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Power;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind;
using Robust.Server.Player;

namespace Content.Server._Starlight.Holograms;

public sealed partial class HologramServerSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly HologramSystem _hologram = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public const string TagHoloDisk = "HoloDisk";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HologramDiskComponent, AfterInteractEvent>(DiskOnAfterInteract);
        SubscribeLocalEvent<HologramServerComponent, PowerChangedEvent>(ServerOnPowerChanged);
    }

    /// <summary>
    ///     Called when the server's power state changes
    /// </summary>
    private void ServerOnPowerChanged(EntityUid uid, HologramServerComponent component, ref PowerChangedEvent args)
    {
        // If the server loses power, kill the hologram
        if (!args.Powered && Exists(component.LinkedHologram))
        {
            _hologram.DoKillHologram(component.LinkedHologram.Value);
            component.LinkedHologram = null;
        }
    }

    private void DiskOnAfterInteract(EntityUid uid, HologramDiskComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || !TryComp<MindContainerComponent>(args.Target, out var targetMind))
            return;

        if (targetMind.Mind == null)
        {
            _popup.PopupEntity(Loc.GetString("system-hologram-disk-mind-none"), args.Target.Value, args.User);
            args.Handled = true;
            return;
        }

        // Check if the target is dead
        if (_mobState.IsAlive(args.Target.Value) || _mobState.IsCritical(args.Target.Value))
        {
            _popup.PopupEntity(Loc.GetString("system-hologram-disk-target-alive"), args.Target.Value, args.User);
            args.Handled = true;
            return;
        }

        // Get the player session
        if (!_mind.TryGetMind(args.Target.Value, out var mindId, out var mind) || 
            mind.UserId == null || 
            !_playerManager.TryGetSessionById(mind.UserId.Value, out var client))
        {
            _popup.PopupEntity(Loc.GetString("system-hologram-disk-no-client"), args.Target.Value, args.User);
            args.Handled = true;
            return;
        }

        // Store the disk UID in the component temporarily
        component.PendingUser = args.User;
        component.PendingMind = mindId;
        
        // Send popup to request consent
        _popup.PopupEntity(Loc.GetString("system-hologram-disk-consent-request"), args.Target.Value, client);
        _popup.PopupEntity(Loc.GetString("system-hologram-disk-consent-sent"), args.Target.Value, args.User);
        args.Handled = true;
    }

    internal void SaveMindToDisk(EntityUid diskUid, EntityUid mindId)
    {
        if (!TryComp<HologramDiskComponent>(diskUid, out var component))
            return;

        component.HoloMind = mindId;
        
        // Transfer the mind from the body to the disk
        if (TryComp<MindComponent>(mindId, out var mind))
        {
            // Get character name before transfer
            var characterName = mind.CharacterName ?? "Unknown";
            
            // Ensure disk has MindContainer
            var mindContainer = EnsureComp<MindContainerComponent>(diskUid);
            
            // Transfer mind to the disk
            _mind.TransferTo(mindId, diskUid, ghostCheckOverride: true, createGhost: false, mind: mind);
            
            // Rename the disk to include the character's name
            _metaData.SetEntityName(diskUid, Loc.GetString("hologram-disk-mind-name", ("name", characterName)));
        }
        
        if (component.PendingUser != null && Exists(component.PendingUser.Value))
            _popup.PopupEntity(Loc.GetString("system-hologram-disk-mind-saved"), diskUid, component.PendingUser.Value);
        
        component.PendingUser = null;
    }
}
