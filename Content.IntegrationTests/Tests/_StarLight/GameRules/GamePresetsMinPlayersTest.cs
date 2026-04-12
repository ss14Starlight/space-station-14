using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Server.GameTicking.Presets;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._StarLight.GameRules;

[TestFixture]
public sealed class GamePresetsMinPlayersTest
{
    private const string TestPreset = "TestPresetTenPlayers";

    [Test]
    public async Task TestMinPlayers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var compFactory = server.ResolveDependency<IComponentFactory>();
        var presets = protoMan.EnumeratePrototypes<GamePresetPrototype>();

        var errorPresets = new List<string>();

        foreach (var preset in presets)
        {
            if (preset.ID == TestPreset)
                continue; // This preset is specifically for testing and has a MinPlayers value that doesn't match its rules, so ignore it
            var minPlayers = preset.MinPlayers ?? 0;
            if (minPlayers < 0)
                errorPresets.Add($"{preset.ID}: preset MinPlayers is negative ({minPlayers})");
            var minPlayersRules = GetBiggestMinPlayers(preset, protoMan, compFactory, errorPresets);

            if (minPlayers < minPlayersRules)
                errorPresets.Add($"{preset.ID}: preset={minPlayers}, required={minPlayersRules}");
        }

        Assert.That(
            errorPresets.Count,
            Is.Zero,
            $"Found invalid preset/rule min-player configuration(s):{Environment.NewLine}{string.Join(Environment.NewLine, errorPresets)}");

        await pair.CleanReturnAsync();
    }

    private int GetBiggestMinPlayers(GamePresetPrototype preset, IPrototypeManager manager, IComponentFactory factory, List<string> errors)
    {
        var biggest = 0;
        foreach (var rule in preset.Rules)
        {
            if (!manager.TryIndex<EntityPrototype>(rule, out var ruleProto))
            {
                errors.Add($"Rule prototype '{rule}' not found in preset '{preset.ID}'");
                continue;
            }

            if (!TryGetComponent<GameRuleComponent>(ruleProto.Components, factory, out var ruleComponent))
            {
                errors.Add($"Rule '{rule}' in preset '{preset.ID}' has no GameRuleComponent");
                continue;
            }

            if (!ruleComponent.CancelPresetOnTooFewPlayers) // Ignore rules that don't cancel the preset if there are too few players, as they don't require a minimum player count
                continue;

            biggest = Math.Max(biggest, ruleComponent.MinPlayers);
        }
        return biggest;
    }

    private static bool TryGetComponent<T>(ComponentRegistry components, IComponentFactory factory, [NotNullWhen(true)] out T? component) where T : IComponent, new()
    {
        if (!components.TryGetValue(factory.GetComponentName<T>(), out var componentUnCast))
        {
            component = default;
            return false;
        }

        if (componentUnCast.Component is not T cast)
        {
            component = default;
            return false;
        }

        component = cast;
        return true;
    }
}
