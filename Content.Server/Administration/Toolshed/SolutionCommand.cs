using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Syntax;
using Robust.Shared.Toolshed.TypeParsers;
using System.Linq;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Components.SolutionManager; // Starlight
using Robust.Shared.Containers; // Starlight

namespace Content.Server.Administration.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SolutionCommand : ToolshedCommand
{
    private SharedSolutionContainerSystem? _solutionContainer;
    private SharedContainerSystem? _container; // Starlight

    [CommandImplementation("get")]
    public SolutionRef? Get([PipedArgument] EntityUid input, string name)
    {
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();

        if (_solutionContainer.TryGetSolution(input, name, out var solution))
            return new SolutionRef(solution.Value);

        return null;
    }

    [CommandImplementation("get")]
    public IEnumerable<SolutionRef> Get([PipedArgument] IEnumerable<EntityUid> input, string name)
    {
        return input.Select(x => Get(x, name)).Where(x => x is not null).Cast<SolutionRef>();
    }

    [CommandImplementation("adjreagent")]
    public SolutionRef AdjReagent(
            [PipedArgument] SolutionRef input,
            ProtoId<ReagentPrototype> proto,
            float amount
        )
    {
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();

        // Convert float to FixedPoint2
        var amountFixed = FixedPoint2.New(amount);

        if (amountFixed > 0)
        {
            _solutionContainer.TryAddReagent(input.Solution, proto, amountFixed, out _);
        }
        else if (amountFixed < 0)
        {
            _solutionContainer.RemoveReagent(input.Solution, proto, -amountFixed);
        }

        return input;
    }

    [CommandImplementation("adjreagent")]
    public IEnumerable<SolutionRef> AdjReagent(
            [PipedArgument] IEnumerable<SolutionRef> input,
            ProtoId<ReagentPrototype> name,
            float amount
        )
        => input.Select(x => AdjReagent(x, name, amount));

    //Starlight begin
    [CommandImplementation("adjcapacity")]
    public SolutionRef AdjCapacity([PipedArgument] SolutionRef input, float amount)
    {
        if (amount < 0) return input;
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();
        _solutionContainer.SetCapacity(input.Solution, amount);
        return input;
    }

    [CommandImplementation("adjcapacity")]
    public IEnumerable<SolutionRef> AdjCapacity([PipedArgument] IEnumerable<SolutionRef> input, float amount) =>
        input.Select(x => AdjCapacity(x, amount));

    [CommandImplementation("adjtemperature")]
    public SolutionRef AdjTemperature([PipedArgument] SolutionRef input, float temp)
    {
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();
        
        _solutionContainer.SetTemperature(input.Solution, temp);
        return input;
    }

    [CommandImplementation("adjtemperature")]
    public IEnumerable<SolutionRef> AdjTemperature([PipedArgument] IEnumerable<SolutionRef> input, float temp) =>
        input.Select(x => AdjTemperature(x, temp));

    [CommandImplementation("adjthermalenergy")]
    public SolutionRef AdjThermalEnergy([PipedArgument] SolutionRef input, float energy)
    {
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();
        
        _solutionContainer.SetThermalEnergy(input.Solution, energy);
        return input;
    }

    [CommandImplementation("adjthermalenergy")]
    public IEnumerable<SolutionRef> AdjThermalEnergy([PipedArgument] IEnumerable<SolutionRef> input, float energy) =>
        input.Select(x => AdjThermalEnergy(x, energy));

    [CommandImplementation("create")]
    public SolutionRef? Create([PipedArgument] EntityUid uid, string name)
    {
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();
        var sMgr = EnsureComp<SolutionContainerManagerComponent>(uid);
        if(_solutionContainer.EnsureSolutionEntity((uid, sMgr), name, out var ent))
            return new SolutionRef(ent.Value);
        return null;
    }

    [CommandImplementation("delete")]
    public EntityUid Delete([PipedArgument] EntityUid uid, string name)
    {
        _solutionContainer ??= GetSys<SharedSolutionContainerSystem>();
        if (_solutionContainer.TryGetSolution(uid, name, out var solution))
            QDel(solution.Value.Owner);
        return uid;
    }

    [CommandImplementation("create")]
    public IEnumerable<SolutionRef?> Create([PipedArgument] IEnumerable<EntityUid> uid, string name) =>
        uid.Select(x => Create(x, name)).Where(x => x is not null);

    [CommandImplementation("delete")]
    public IEnumerable<EntityUid> Delete([PipedArgument] IEnumerable<EntityUid> uid, string name) =>
        uid.Select(x => Delete(x, name));
    
    [CommandImplementation("delete")]
    public EntityUid? Delete([PipedArgument] SolutionRef solution)
    {
        _container ??= GetSys<SharedContainerSystem>();
        QDel(solution.Solution.Owner);
        if (!_container.TryGetContainingContainer(solution.Solution.Owner, out var container)) return null;
        return container.Owner;
    }
    //Starlight end
}

public readonly record struct SolutionRef(Entity<SolutionComponent> Solution)
{
    public override string ToString()
    {
        return $"{Solution.Owner} {Solution.Comp.Solution}";
    }
}
