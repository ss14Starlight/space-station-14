using System.Collections.Generic;
using System.Linq;
using Content.Server.Access.Systems;
using Content.Shared.Access;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Server.Access;

/// <summary>
/// Regression tests for issue #3813: the ID card console rejected entire access
/// writes when the submission echoed a tag outside the console's access groups
/// (making such IDs completely uneditable) or contained any single change the
/// privileged ID could not make (making "grant all" / job presets silently fail).
/// </summary>
[TestFixture]
[TestOf(typeof(IdCardConsoleSystem))]
public sealed class IdCardConsoleAccessChangeTest
{
    private static readonly HashSet<ProtoId<AccessLevelPrototype>> ConsoleGroups =
        ["Command", "Engineering", "ChiefEngineer", "Theatre", "Maintenance"];

    private static HashSet<ProtoId<AccessLevelPrototype>> Tags(params string[] tags)
        => tags.Select(t => (ProtoId<AccessLevelPrototype>)t).ToHashSet();

    private static List<ProtoId<AccessLevelPrototype>> Submit(params string[] tags)
        => tags.Select(t => (ProtoId<AccessLevelPrototype>)t).ToList();

    [Test]
    public void EchoedTagOutsideConsoleGroupsDoesNotBlockEdit()
    {
        // Target ID carries CentralCommand, which this console's groups don't contain.
        // The client echoes it back on submit; the edit (add Theatre) must still apply.
        var (final, disallowed, attemptedUnknown) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: Tags("Command", "CentralCommand"),
            newAccessList: Submit("Command", "CentralCommand", "Theatre"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Command", "Theatre"));

        Assert.Multiple(() =>
        {
            Assert.That(final, Is.EquivalentTo(Tags("Command", "CentralCommand", "Theatre")));
            Assert.That(disallowed, Is.Empty);
            Assert.That(attemptedUnknown, Is.False);
        });
    }

    [Test]
    public void DisallowedChangeIsRevertedWhileAllowedChangesApply()
    {
        // Job preset drops ChiefEngineer (which the operator doesn't hold) and adds
        // Theatre. Previously the whole write was rejected; now ChiefEngineer is kept
        // and Theatre is still granted.
        var (final, disallowed, _) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: Tags("Engineering", "ChiefEngineer"),
            newAccessList: Submit("Engineering", "Theatre"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Engineering", "Theatre"));

        Assert.Multiple(() =>
        {
            Assert.That(final, Is.EquivalentTo(Tags("Engineering", "ChiefEngineer", "Theatre")));
            Assert.That(disallowed, Is.EquivalentTo(Tags("ChiefEngineer")));
        });
    }

    [Test]
    public void DisallowedAdditionIsReverted()
    {
        var (final, disallowed, _) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: Tags("Theatre"),
            newAccessList: Submit("Theatre", "ChiefEngineer"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Theatre"));

        Assert.Multiple(() =>
        {
            Assert.That(final, Is.EquivalentTo(Tags("Theatre")));
            Assert.That(disallowed, Is.EquivalentTo(Tags("ChiefEngineer")));
        });
    }

    [Test]
    public void TagUnknownToConsoleCannotBeAdded()
    {
        // Submitting a tag outside the console's groups that the target doesn't already
        // have is a smuggle attempt: it must be ignored and flagged.
        var (final, _, attemptedUnknown) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: Tags("Command"),
            newAccessList: Submit("Command", "CentralCommand"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Command", "CentralCommand"));

        Assert.Multiple(() =>
        {
            Assert.That(final, Is.EquivalentTo(Tags("Command")));
            Assert.That(attemptedUnknown, Is.True);
        });
    }

    [Test]
    public void TagOutsideConsoleGroupsCannotBeRemoved()
    {
        // Omitting an out-of-group tag from the submission must not strip it:
        // this console doesn't manage that access.
        var (final, _, _) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: Tags("Command", "CentralCommand"),
            newAccessList: Submit("Command"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Command", "CentralCommand"));

        Assert.That(final, Is.EquivalentTo(Tags("Command", "CentralCommand")));
    }

    [Test]
    public void AllowedGrantAndRevokeApply()
    {
        var (final, disallowed, attemptedUnknown) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: Tags("Maintenance"),
            newAccessList: Submit("Engineering", "Theatre"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Maintenance", "Engineering", "Theatre"));

        Assert.Multiple(() =>
        {
            Assert.That(final, Is.EquivalentTo(Tags("Engineering", "Theatre")));
            Assert.That(disallowed, Is.Empty);
            Assert.That(attemptedUnknown, Is.False);
        });
    }

    [Test]
    public void UnchangedEchoIsNoOp()
    {
        var old = Tags("Command", "CentralCommand");
        var (final, disallowed, _) = IdCardConsoleSystem.ComputeAccessChange(
            oldTags: old,
            newAccessList: Submit("Command", "CentralCommand"),
            consoleGroupTags: ConsoleGroups,
            privilegedPerms: Tags("Command"));

        Assert.Multiple(() =>
        {
            Assert.That(final, Is.EquivalentTo(old));
            Assert.That(disallowed, Is.Empty);
        });
    }
}
