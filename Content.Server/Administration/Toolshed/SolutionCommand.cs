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

namespace Content.Server.Administration.Toolshed;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class SolutionCommand : ToolshedCommand
{
    private SharedSolutionContainerSystem? _solutionContainer;

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
    //Starlight end
}

public readonly record struct SolutionRef(Entity<SolutionComponent> Solution)
{
    public override string ToString()
    {
        return $"{Solution.Owner} {Solution.Comp.Solution}";
    }
}
