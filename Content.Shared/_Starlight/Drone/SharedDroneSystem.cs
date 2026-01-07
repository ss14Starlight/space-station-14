using Content.Shared.Interaction.Events;
using Content.Shared.Tag;
using Robust.Shared.Serialization;

namespace Content.Shared.Drone;

/// <summary>
/// Handles drone interaction restrictions based on tags.
/// </summary>
public abstract class SharedDroneSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;
    
    public override void Initialize()
        => SubscribeLocalEvent<DroneComponent, InteractionAttemptEvent>(OnInteractionAttempt);

    private void OnInteractionAttempt(EntityUid uid, DroneComponent component, ref InteractionAttemptEvent args)
    {
        if (args.Target == null)
            return;

        var target = args.Target.Value;
        
        // Check blacklist first - if any blacklisted tag is present, deny
        foreach (var blacklistedTag in component.InteractionBlacklist)
        {
            if (_tagSystem.HasTag(target, blacklistedTag))
            {
                args.Cancelled = true;
                return;
            }
        }
        
        // If whitelist is empty, allow everything not blacklisted
        if (component.InteractionWhitelist.Count == 0)
            return;
        
        // Check whitelist - if any whitelisted tag is present, allow
        foreach (var whitelistedTag in component.InteractionWhitelist)
        {
            if (_tagSystem.HasTag(target, whitelistedTag))
                return; // Allowed!
        }
        
        // No whitelisted tags found, deny interaction
        args.Cancelled = true;
    }

    [Serializable, NetSerializable]
    public enum DroneVisuals : byte
    {
        Status
    }
    
    [Serializable, NetSerializable]
    public enum DroneStatus : byte
    {
        Off,
        On
    }
}