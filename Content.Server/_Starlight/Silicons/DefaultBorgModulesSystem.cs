using System.Linq;
using Content.Server.Silicons.Borgs;
using Content.Shared._Starlight.Silicons.Borgs;
using Content.Shared.Containers;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Silicons;

public sealed class DefaultBorgModulesSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BorgSystem _borg = default!;
    
    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<DefaultBorgModulesComponent, MapInitEvent>(OnMapInit, after: [typeof(ContainerFillSystem)]);
    }

    private void OnMapInit(EntityUid uid, DefaultBorgModulesComponent comp, MapInitEvent ev)
    {
        if (!TryComp<BorgChassisComponent>(uid, out var chassis) 
            || !TryComp<ContainerManagerComponent>(uid, out var manager)) 
            return;
        var xform = Transform(uid);
        // get existing protos
        var existingPrototypes = chassis.ModuleContainer.ContainedEntities.Select(ent => MetaData(ent).EntityPrototype?.ID).ToList();
        // get existing modules that we want defaulted
        var existingModules = chassis.ModuleContainer.ContainedEntities.Where(ent =>
        {
            var protoId = MetaData(ent).EntityPrototype?.ID;
            return protoId is not null && comp.Modules.Contains(protoId);
        });
        // get list of protos that we want to be default that dont exist already
        var needToInsert = comp.Modules.Where(x => !existingPrototypes.Contains(x)).ToList();
        // insert the ones that dont exist, default them
        foreach (var id in needToInsert)
        {
            if (!_proto.Resolve(id, out _)) continue;
            var ent = SpawnInContainerOrDrop(id, uid, chassis.ModuleContainer.ID, xform, manager);
            if (!TryComp<BorgModuleComponent>(ent, out var module))
            {
                QueueDel(ent);
                continue;
            }

            _borg.SetBorgModuleDefault((ent, module), true);
        }
        // default the remaining target modules
        foreach (var ent in existingModules)
        {
            if (!TryComp<BorgModuleComponent>(ent, out var module)) continue;
            _borg.SetBorgModuleDefault((ent, module), true);
        }
    }
}