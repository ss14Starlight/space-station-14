using System.Linq;
using Content.Server.Administration;
using Content.Server.Polymorph.Systems;
using Content.Shared.Administration;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;
//Starlight begin
using Content.Server.Polymorph.Components;
using System.Diagnostics.CodeAnalysis;
using Content.Server._Starlight.Administration.Systems.Commands;
using Robust.Shared.Audio;
//Starlight end

namespace Content.Server.Polymorph.Toolshed;

/// <summary>
///     Polymorphs the given entity(s) into the target morph.
/// </summary>
[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class PolymorphCommand : ToolshedCommand
{
    private PolymorphSystem? _system;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _factory = default!; // Starlight

    [CommandImplementation("proto")] // Starlight-edit
    public EntityUid? Polymorph(
            [PipedArgument] EntityUid input,
            ProtoId<PolymorphPrototype> protoId
        )
    {
        _system ??= GetSys<PolymorphSystem>();

        var prototype = _proto.Index(protoId);

        return _system.PolymorphEntity(input, prototype.Configuration);
    }

    [CommandImplementation("proto")] // Starlight-edit
    public IEnumerable<EntityUid> Polymorph(
            [PipedArgument] IEnumerable<EntityUid> input,
            ProtoId<PolymorphPrototype> protoId
        )
        => input.Select(x => Polymorph(x, protoId)).Where(x => x is not null).Select(x => (EntityUid)x!);
    
    //Starlight begin
    #region Single Entity
    /// <summary>
    /// Marker to begin a sequence of polymorph configuration instructions, will attach a <see cref="PolymorphSetupComponent"/> to the entity.
    /// </summary>
    [CommandImplementation("begin")]
    public EntityUid Begin([PipedArgument] EntityUid uid)
    {
        EnsureComp<PolymorphSetupComponent>(uid);
        return uid;
    }

    /// <summary>
    /// Set the prototype that the entity will polymorph into.
    /// </summary>
    [CommandImplementation("setproto")]
    public EntityUid SetPrototype([PipedArgument] EntityUid uid, ProtoId<EntityPrototype> proto)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.Entity = proto.Id;
        return uid;
    }

    /// <summary>
    /// Set a prototype to spawn on top of the polymorphed entity, typically this is used to create special effects.
    /// </summary>
    [CommandImplementation("seteffect")]
    public EntityUid SetEffect([PipedArgument] EntityUid uid, ProtoId<EntityPrototype> proto)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.EffectProto = proto.Id;
        return uid;
    }

    /// <summary>
    /// Set how long in seconds must be waited before being able to activate this specific polymorph again.
    /// </summary>
    [CommandImplementation("setdelay")]
    public EntityUid SetDelay([PipedArgument] EntityUid uid, int delay)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.Delay = delay;
        return uid;
    }

    /// <summary>
    /// Set the duration the polymorph should last for in seconds before automatically reverting.
    /// </summary>
    [CommandImplementation("setduration")]
    public EntityUid SetDuration([PipedArgument] EntityUid uid, int duration)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.Duration = duration;
        return uid;
    }

    /// <summary>
    /// Set to make so the polymorph cannot be activated or canceled by the entity itself.
    /// </summary>
    [CommandImplementation("setforced")]
    public EntityUid SetForced([PipedArgument] EntityUid uid, bool forced)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.Forced = forced;
        return uid;
    }
    
    /// <summary>
    /// Set to transfer the damage from the current entity to the polymorphed entity.
    /// </summary>
    [CommandImplementation("settransferdamage")]
    public EntityUid SetTransferDamage([PipedArgument] EntityUid uid, bool transfer)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.TransferDamage = transfer;
        return uid;
    }
    
    /// <summary>
    /// Set to make the polymorphed entity inherit the name of the original.
    /// </summary>
    [CommandImplementation("settransfername")]
    public EntityUid SetTransferName([PipedArgument] EntityUid uid, bool transfer)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.TransferName = transfer;
        return uid;
    }
    
    /// <summary>
    /// Set whether to transfer things like hair, skin color, height, etc. to the polymorphed entity.
    /// </summary>
    [CommandImplementation("settransferappearance")]
    public EntityUid SetTransferHumanoidAppearance([PipedArgument] EntityUid uid, bool transfer)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.TransferHumanoidAppearance = transfer;
        return uid;
    }
    
    /// <summary>
    /// Set to determine how the entity's inventory will transfer to the polymorphed entity.
    /// </summary>
    [CommandImplementation("setinventory")]
    public EntityUid SetInventory([PipedArgument] EntityUid uid, PolymorphInventoryChange transferType)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.Inventory = transferType;
        return uid;
    }
    
    /// <summary>
    /// Set whether to revert the polymorph when the entity enters a critical state or not.
    /// </summary>
    [CommandImplementation("setrevertoncrit")]
    public EntityUid SetRevertOnCrit([PipedArgument] EntityUid uid, bool revert)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.RevertOnCrit = revert;
        return uid;
    }
    
    /// <summary>
    /// Set whether to revert the polymorph when the entity is killed or not.
    /// </summary>
    [CommandImplementation("setrevertondeath")]
    public EntityUid SetRevertOnDeath([PipedArgument] EntityUid uid, bool revert)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.RevertOnDeath = revert;
        return uid;
    }
    
    /// <summary>
    /// Set whether to revert the polymorph when the entity is deleted or not.
    /// </summary>
    [CommandImplementation("setrevertondelete")]
    public EntityUid SetRevertOnDelete([PipedArgument] EntityUid uid, bool revert)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.RevertOnDelete = revert;
        return uid;
    }
    
    /// <summary>
    /// Set whether to revert the polymorph when the entity is eaten or not.
    /// </summary>
    [CommandImplementation("setrevertoneat")]
    public EntityUid SetRevertOnEat([PipedArgument] EntityUid uid, bool revert)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.RevertOnEat = revert;
        return uid;
    }
    
    /// <summary>
    /// Set whether to allow repeated polymorphs or not.
    /// </summary>
    [CommandImplementation("setallowrepeats")]
    public EntityUid SetAllowRepeats([PipedArgument] EntityUid uid, bool allow)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.AllowRepeatedMorphs = allow;
        return uid;
    }
    
    /// <summary>
    /// Set to allow the polymorph to happen even if AllowRepeatedMorphs is true.
    /// </summary>
    [CommandImplementation("setignoreallowrepeats")]
    public EntityUid SetIgnoreAllowRepeats([PipedArgument] EntityUid uid, bool ignore)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.IgnoreAllowRepeatedMorphs = ignore;
        return uid;
    }
    
    /// <summary>
    /// Set the cooldown in seconds before another polymorph can take place.
    /// </summary>
    [CommandImplementation("setcooldown")]
    public EntityUid SetCooldown([PipedArgument] EntityUid uid, float seconds)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.Cooldown = TimeSpan.FromSeconds(seconds);
        return uid;
    }
    
    /// <summary>
    /// Set the sound that plays when entering the polymorph.
    /// </summary>
    [CommandImplementation("setentersound")]
    public EntityUid SetEnterSound([PipedArgument] EntityUid uid, SoundType type, string path)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.PolymorphSound =
            type is SoundType.Path ? new SoundPathSpecifier(path) : new SoundCollectionSpecifier(path);
        return uid;
    }
    
    /// <summary>
    /// Set the sound that plays when exiting the polymorph.
    /// </summary>
    [CommandImplementation("setexitsound")]
    public EntityUid SetExitSound([PipedArgument] EntityUid uid, SoundType type, string path)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.ExitPolymorphSound =
            type is SoundType.Path ? new SoundPathSpecifier(path) : new SoundCollectionSpecifier(path);
        return uid;
    }
    
    /// <summary>
    /// Clear the sound that plays when entering the polymorph.
    /// </summary>
    [CommandImplementation("clearentersound")]
    public EntityUid ClearEnterSound([PipedArgument] EntityUid uid)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.PolymorphSound = null;
        return uid;
    }
    
    /// <summary>
    /// Clear the sound that plays when exiting the polymorph.
    /// </summary>
    [CommandImplementation("clearexitsound")]
    public EntityUid ClearExitSound([PipedArgument] EntityUid uid)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.ExitPolymorphSound = null;
        return uid;
    }
    
    /// <summary>
    /// Set the popup that appears when entering the polymorph.
    /// </summary>
    [CommandImplementation("setenterpopup")]
    public EntityUid SetEnterPopup([PipedArgument] EntityUid uid, string? popup)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.PolymorphPopup = popup;
        return uid;
    }
    
    /// <summary>
    /// Set the popup that appears when exiting the polymorph.
    /// </summary>
    [CommandImplementation("setexitpopup")]
    public EntityUid SetExitPopup([PipedArgument] EntityUid uid, string? popup)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.ExitPolymorphPopup = popup;
        return uid;
    }
    
    /// <summary>
    /// Clear the list of components to copy to the polymorph.
    /// </summary>
    [CommandImplementation("clearcopycomp")]
    public EntityUid ClearCopiedComponents([PipedArgument] EntityUid uid)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        config.Config.CopiedComponents.Clear();
        return uid;
    }
    
    /// <summary>
    /// Add an entry to the list of components to copy to the polymorph.
    /// </summary>
    [CommandImplementation("addcopycomp")]
    public EntityUid AddCopiedComponent([PipedArgument] EntityUid uid, string componentName)
    {
        if (!_factory.TryGetRegistration(componentName, out var reg) ||
            !EnsureConfig(uid, out var config)) return uid;
        config.Config.CopiedComponents.Add(reg.Name);
        return uid;
    }
    
    /// <summary>
    /// Remove an entry from the list of components to copy to the polymorph.
    /// </summary>
    [CommandImplementation("rmcopycomp")]
    public EntityUid RemoveCopiedComponent([PipedArgument] EntityUid uid, string componentName)
    {
        if (!_factory.TryGetRegistration(componentName, out var reg) ||
            !EnsureConfig(uid, out var config)) return uid;
        config.Config.CopiedComponents.Remove(reg.Name);
        return uid;
    }
    
    /// <summary>
    /// Instantly apply the polymorph and finish.
    /// </summary>
    [CommandImplementation("apply")]
    public EntityUid Apply([PipedArgument] EntityUid uid)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        _system?.PolymorphEntity(uid, config.Config);
        RemComp<PolymorphSetupComponent>(uid);
        return uid;
    }
    
    /// <summary>
    /// Instantly apply the polymorph and finish, returning the new entity.
    /// </summary>
    [CommandImplementation("applyget")]
    public EntityUid ApplyGet([PipedArgument] EntityUid uid)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        var ent = _system?.PolymorphEntity(uid, config.Config);
        RemComp<PolymorphSetupComponent>(uid);
        return ent ?? EntityUid.Invalid;
    }

    /// <summary>
    /// Add a polymorph action to the entity using the current polymorph setup chain.
    /// Call polymorph:apply or polymorph:finish afterward.
    /// </summary>
    [CommandImplementation("addaction")]
    public EntityUid AddAction([PipedArgument] EntityUid uid, string instanceId)
    {
        if(!EnsureConfig(uid, out var config)) return uid;
        _system?.CreatePolymorphAction(instanceId, config.Config, uid, null);
        return uid;
    }

    /// <summary>
    /// Add a prototyped polymorph action to the entity.
    /// </summary>
    [CommandImplementation("addactionproto")]
    public EntityUid AddActionPrototype([PipedArgument] EntityUid uid, ProtoId<PolymorphPrototype> protoId)
    {
        _system ??= EntitySystemManager.GetEntitySystem<PolymorphSystem>();
        if (!TryComp<PolymorphableComponent>(uid, out var comp)) return uid;
        _system.CreatePolymorphAction(protoId, (uid, comp));
        return uid;
    }

    /// <summary>
    /// Remove a polymorph action from the entity that was added via polymorph:addaction.
    /// </summary>
    [CommandImplementation("rmaction")]
    public EntityUid RemoveAction([PipedArgument] EntityUid uid, string instanceId)
    {
        _system ??= EntitySystemManager.GetEntitySystem<PolymorphSystem>();
        if (!TryComp<PolymorphableComponent>(uid, out var comp)) return uid;
        _system.RemovePolymorphAction(instanceId, uid, comp);
        return uid;
    }

    /// <summary>
    /// Remove a prototyped polymorph action from the entity.
    /// </summary>
    [CommandImplementation("rmactionproto")]
    public EntityUid RemoveActionPrototype([PipedArgument] EntityUid uid, ProtoId<PolymorphPrototype> protoId)
    {
        _system ??= EntitySystemManager.GetEntitySystem<PolymorphSystem>();
        if (!TryComp<PolymorphableComponent>(uid, out var comp)) return uid;
        _system.RemovePolymorphAction(protoId, (uid, comp));
        return uid;
    }
    
    /// <summary>
    /// Revert to the previous x entity, if possible.
    /// </summary>
    [CommandImplementation("revert")]
    public EntityUid? Revert([PipedArgument] EntityUid uid, int depth)
    {
        _system ??= EntitySystemManager.GetEntitySystem<PolymorphSystem>();
        EntityUid? newUid = uid;
        for (var i = 0; i < depth; i++)
        {
            if (newUid is null || !HasComp<PolymorphedEntityComponent>(newUid.Value)) break;
            newUid = _system.Revert(newUid.Value);
        }
        return newUid;
    }

    /// <summary>
    /// Reset the entity's polymorph to their original state.
    /// </summary>
    [CommandImplementation("reset")]
    public EntityUid? Reset([PipedArgument] EntityUid uid)
    {
        _system ??= EntitySystemManager.GetEntitySystem<PolymorphSystem>();
        EntityUid? newUid = uid;
        while (true)
        {
            if (newUid is null || !HasComp<PolymorphedEntityComponent>(newUid.Value)) return newUid;
            newUid = _system.Revert(newUid.Value);
        }
    }

    /// <summary>
    /// Marks this polymorph setup chain as complete, cleaning up and removing the component.
    /// </summary>
    [CommandImplementation("finish")]
    public EntityUid Finish([PipedArgument] EntityUid uid)
    {
        RemComp<PolymorphSetupComponent>(uid);
        return uid;
    }
    #endregion
    
    #region Multiple Entities
    [CommandImplementation("begin")]
    public IEnumerable<EntityUid> Begin([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Begin);
    
    [CommandImplementation("setproto")]
    public IEnumerable<EntityUid> SetPrototype([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<EntityPrototype> proto)
        => uid.Select(x=>SetPrototype(x, proto));
    
    [CommandImplementation("seteffect")]
    public IEnumerable<EntityUid> SetEffect([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<EntityPrototype> proto)
        => uid.Select(x=>SetEffect(x, proto));
    
    [CommandImplementation("setdelay")]
    public IEnumerable<EntityUid> SetDelay([PipedArgument] IEnumerable<EntityUid> uid, int delay)
        => uid.Select(x=>SetDelay(x, delay));
    
    [CommandImplementation("setduration")]
    public IEnumerable<EntityUid> SetDuration([PipedArgument] IEnumerable<EntityUid> uid, int duration)
        => uid.Select(x=>SetDuration(x, duration));
    
    [CommandImplementation("setforced")]
    public IEnumerable<EntityUid> SetForced([PipedArgument] IEnumerable<EntityUid> uid, bool forced)
        => uid.Select(x=>SetForced(x, forced));
    
    [CommandImplementation("settransferdamage")]
    public IEnumerable<EntityUid> SetTransferDamage([PipedArgument] IEnumerable<EntityUid> uid, bool transfer)
        => uid.Select(x=>SetTransferDamage(x, transfer));
    
    [CommandImplementation("settransfername")]
    public IEnumerable<EntityUid> SetTransferName([PipedArgument] IEnumerable<EntityUid> uid, bool transfer)
        => uid.Select(x=>SetTransferName(x, transfer));
    
    [CommandImplementation("settransferappearance")]
    public IEnumerable<EntityUid> SetTransferHumanoidAppearance([PipedArgument] IEnumerable<EntityUid> uid, bool transfer)
        => uid.Select(x=>SetTransferHumanoidAppearance(x, transfer));
    
    [CommandImplementation("setinventory")]
    public IEnumerable<EntityUid> SetInventory([PipedArgument] IEnumerable<EntityUid> uid, PolymorphInventoryChange transferType)
        => uid.Select(x=>SetInventory(x, transferType));
    
    [CommandImplementation("setrevertoncrit")]
    public IEnumerable<EntityUid> SetRevertOnCrit([PipedArgument] IEnumerable<EntityUid> uid, bool revert)
        => uid.Select(x=>SetRevertOnCrit(x, revert));
    
    [CommandImplementation("setrevertondeath")]
    public IEnumerable<EntityUid> SetRevertOnDeath([PipedArgument] IEnumerable<EntityUid> uid, bool revert)
        => uid.Select(x=>SetRevertOnDeath(x, revert));
    
    [CommandImplementation("setrevertondelete")]
    public IEnumerable<EntityUid> SetRevertOnDelete([PipedArgument] IEnumerable<EntityUid> uid, bool revert)
        => uid.Select(x=>SetRevertOnDelete(x, revert));
    
    [CommandImplementation("setrevertoneat")]
    public IEnumerable<EntityUid> SetRevertOnEat([PipedArgument] IEnumerable<EntityUid> uid, bool revert)
        => uid.Select(x=>SetRevertOnEat(x, revert));
    
    [CommandImplementation("setallowrepeats")]
    public IEnumerable<EntityUid> SetAllowRepeats([PipedArgument] IEnumerable<EntityUid> uid, bool allow)
        => uid.Select(x=>SetAllowRepeats(x, allow));
    
    [CommandImplementation("setignoreallowrepeats")]
    public IEnumerable<EntityUid> SetIgnoreAllowRepeats([PipedArgument] IEnumerable<EntityUid> uid, bool ignore)
        => uid.Select(x=>SetIgnoreAllowRepeats(x, ignore));
    
    [CommandImplementation("setcooldown")]
    public IEnumerable<EntityUid> SetCooldown([PipedArgument] IEnumerable<EntityUid> uid, float seconds)
        => uid.Select(x=>SetCooldown(x, seconds));
    
    [CommandImplementation("setentersound")]
    public IEnumerable<EntityUid> SetEnterSound([PipedArgument] IEnumerable<EntityUid> uid, SoundType type, string path)
        => uid.Select(x=>SetEnterSound(x, type, path));
    
    [CommandImplementation("setexitsound")]
    public IEnumerable<EntityUid> SetExitSound([PipedArgument] IEnumerable<EntityUid> uid, SoundType type, string path)
        => uid.Select(x=>SetExitSound(x, type, path));
    
    [CommandImplementation("clearentersound")]
    public IEnumerable<EntityUid> ClearEnterSound([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(ClearEnterSound);
    
    [CommandImplementation("clearexitsound")]
    public IEnumerable<EntityUid> ClearExitSound([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(ClearExitSound);
    
    [CommandImplementation("setenterpopup")]
    public IEnumerable<EntityUid> SetEnterPopup([PipedArgument] IEnumerable<EntityUid> uid, string? popup)
        => uid.Select(x=>SetEnterPopup(x, popup));
    
    [CommandImplementation("setexitpopup")]
    public IEnumerable<EntityUid> SetExitPopup([PipedArgument] IEnumerable<EntityUid> uid, string? popup)
        => uid.Select(x=>SetExitPopup(x, popup));
    
    [CommandImplementation("clearcopycomp")]
    public IEnumerable<EntityUid> ClearCopiedComponents([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(ClearCopiedComponents);
    
    [CommandImplementation("addcopycomp")]
    public IEnumerable<EntityUid> AddCopiedComponent([PipedArgument] IEnumerable<EntityUid> uid, string componentName)
        => uid.Select(x=>AddCopiedComponent(x, componentName));

    [CommandImplementation("rmcopycomp")]
    public IEnumerable<EntityUid> RemoveCopiedComponent([PipedArgument] IEnumerable<EntityUid> uid, string componentName)
        => uid.Select(x => RemoveCopiedComponent(x, componentName));
    
    [CommandImplementation("apply")]
    public IEnumerable<EntityUid> Apply([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Apply);
    
    [CommandImplementation("applyget")]
    public IEnumerable<EntityUid> ApplyGet([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(ApplyGet);
    
    [CommandImplementation("addaction")]
    public IEnumerable<EntityUid> AddAction([PipedArgument] IEnumerable<EntityUid> uid, string instanceId)
        => uid.Select(x=>AddAction(x, instanceId));
    
    [CommandImplementation("addactionproto")]
    public IEnumerable<EntityUid> AddActionPrototype([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<PolymorphPrototype> protoId)
        => uid.Select(x=>AddActionPrototype(x, protoId));
    
    [CommandImplementation("rmaction")]
    public IEnumerable<EntityUid> RemoveAction([PipedArgument] IEnumerable<EntityUid> uid, string instanceId)
        => uid.Select(x=>RemoveAction(x, instanceId));
    
    [CommandImplementation("rmactionproto")]
    public IEnumerable<EntityUid> RemoveActionPrototype([PipedArgument] IEnumerable<EntityUid> uid, ProtoId<PolymorphPrototype> protoId)
        => uid.Select(x=>RemoveActionPrototype(x, protoId));
    
    [CommandImplementation("revert")]
    public IEnumerable<EntityUid?> Revert([PipedArgument] IEnumerable<EntityUid> uid, int depth)
        => uid.Select(x=>Revert(x, depth));
    
    [CommandImplementation("reset")]
    public IEnumerable<EntityUid?> Reset([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Reset);
    
    [CommandImplementation("finish")]
    public IEnumerable<EntityUid> Finish([PipedArgument] IEnumerable<EntityUid> uid)
        => uid.Select(Finish);
    #endregion
    
    /// <summary>
    /// Ensures both that a <see cref="PolymorphSetupComponent"/> is on the component, and that the reference to <see cref="PolymorphSystem"/> is valid.
    /// </summary>
    private bool EnsureConfig(EntityUid uid, [NotNullWhen(true)] out PolymorphSetupComponent? config)
    {
        if (!TryComp(uid, out config)) return false;
        _system ??= EntitySystemManager.GetEntitySystem<PolymorphSystem>();
        return true;
    }
    //Starlight end
}

//Starlight begin
// TODO: this should probably be moved into the sound system somewhere but im lazy as fuck lmaooo
public enum SoundType
{
    Path = 0,
    Collection = 1,
}
//Starlight end