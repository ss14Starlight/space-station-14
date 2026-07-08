using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._Starlight.Patches;

/// <summary>
///     Measures per-event and per-component dispatch time by detouring entity bus dispatch and prints summaries after each test.
/// </summary>
internal static class EventTimingSummaryPatch
{
    private static readonly ConcurrentDictionary<string, long> _eventTotals = new();
    private static readonly ConcurrentDictionary<string, long> _componentTotals = new();
    private static Dictionary<string, long> s_eventSnapshot = [];
    private static Dictionary<string, long> s_componentSnapshot = [];
    private static readonly List<IDisposable> _hooks = [];
    private static int s_applied;

    internal static void Apply()
    {
        if (Interlocked.Exchange(ref s_applied, 1) != 0)
            return;

        var eventBusType = typeof(EntityManager).Assembly.GetType("Robust.Shared.GameObjects.EntityEventBus");
        if (eventBusType == null)
        {
            TestContext.Error.WriteLine("[EventTimingSummaryPatch] Could not find EntityEventBus type — patch skipped.");
            return;
        }

        var entDispatch = eventBusType.GetMethod("EntDispatch", BindingFlags.Instance | BindingFlags.NonPublic);
        if (entDispatch == null)
        {
            TestContext.Error.WriteLine("[EventTimingSummaryPatch] Could not find EntDispatch method — patch skipped.");
            return;
        }

        _hooks.Add(new ILHook(entDispatch, InjectEntDispatchTiming));
    }

    internal static Task TakeSnapshot()
    {
        s_eventSnapshot = _eventTotals.ToDictionary(static kv => kv.Key, static kv => kv.Value);
        s_componentSnapshot = _componentTotals.ToDictionary(static kv => kv.Key, static kv => kv.Value);
        return Task.CompletedTask;
    }

    internal static async Task PrintTop10(TextWriter output)
    {
        await PrintTop10(output, "event dispatches", _eventTotals, s_eventSnapshot);
        await PrintTop10(output, "component subscriptions", _componentTotals, s_componentSnapshot);
    }

    private static void InjectEntDispatchTiming(ILContext il)
    {
        var getTimestamp = typeof(System.Diagnostics.Stopwatch).GetMethod(nameof(System.Diagnostics.Stopwatch.GetTimestamp), BindingFlags.Public | BindingFlags.Static);
        var recordTiming = typeof(EventTimingSummaryPatch).GetMethod(nameof(RecordTiming), BindingFlags.NonPublic | BindingFlags.Static);
        if (getTimestamp == null || recordTiming == null)
            return;

        var startTicks = new VariableDefinition(il.Import(typeof(long)));
        il.Body.Variables.Add(startTicks);
        il.Body.InitLocals = true;

        var cursor = new ILCursor(il);
        while (cursor.TryGotoNext(MoveType.Before, instr => instr.OpCode == OpCodes.Callvirt && instr.Operand is Mono.Cecil.MethodReference mr && mr.Name == "Invoke" && mr.DeclaringType.Name.Contains("DirectedEventHandler")))
        {
            var invoke = cursor.Next;
            var compLoad = invoke?.Previous?.Previous;
            if (compLoad?.OpCode != OpCodes.Ldloc && compLoad?.OpCode != OpCodes.Ldloc_S)
            {
                cursor.Goto(invoke.Next, MoveType.Before);
                continue;
            }

            if (!TryGetLocal(compLoad, out var compLocal))
            {
                cursor.Goto(invoke.Next, MoveType.Before);
                continue;
            }

            cursor.Emit(OpCodes.Call, getTimestamp);
            cursor.Emit(OpCodes.Stloc, startTicks);

            cursor.Goto(invoke.Next, MoveType.Before);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.Emit(OpCodes.Ldloc, compLocal);
            cursor.Emit(OpCodes.Ldloc, startTicks);
            cursor.Emit(OpCodes.Call, recordTiming);

            cursor.Goto(invoke.Next, MoveType.After);
        }
    }

    private static bool TryGetLocal(Instruction instruction, out VariableDefinition local)
    {
        local = null!;

        switch (instruction.OpCode.Code)
        {
            case Code.Ldloc:
            case Code.Ldloc_S:
                local = (VariableDefinition) instruction.Operand;
                return true;
            default:
                return false;
        }
    }

    private static void RecordTiming(Type eventType, IComponent component, long startTicks)
    {
        var elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
        var eventName = eventType.Name;
        var componentName = component.GetType().Name;

        _eventTotals.AddOrUpdate(eventName, elapsed, (_, oldValue) => oldValue + elapsed);
        _componentTotals.AddOrUpdate($"{componentName} / {eventName}", elapsed, (_, oldValue) => oldValue + elapsed);
    }

    private static async Task PrintTop10(TextWriter output, string title, ConcurrentDictionary<string, long> current, Dictionary<string, long> snapshot)
    {
        if (current.IsEmpty)
            return;

        var deltas = current
            .Select(kv => (Name: kv.Key, Delta: kv.Value - snapshot.GetValueOrDefault(kv.Key)))
            .Where(x => x.Delta > 0)
            .OrderByDescending(x => x.Delta)
            .Take(10)
            .ToList();

        if (deltas.Count == 0)
            return;

        await output.WriteLineAsync($"  ┌─ Top 10 {title}");
        for (var i = 0; i < deltas.Count; i++)
            await output.WriteLineAsync($"  │ {i + 1,2}. {deltas[i].Name,-55} {deltas[i].Delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency,8:F2} ms");
        await output.WriteLineAsync("  └" + new string('─', 70));
    }
}
