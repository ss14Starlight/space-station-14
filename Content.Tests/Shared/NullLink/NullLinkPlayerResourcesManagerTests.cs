#nullable enable
using System;
using System.Collections.Generic;
using Content.Shared._NullLink;
using NUnit.Framework;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.UnitTesting;

namespace Content.Tests.Shared.NullLink;

[TestFixture]
[TestOf(typeof(SharedNullLinkPlayerResourcesManager))]
public sealed class NullLinkPlayerResourcesManagerTests : RobustUnitTest
{
    private TestResourcesManager _manager = default!;
    private FakeSession _sessionA = default!;
    private FakeSession _sessionB = default!;

    [SetUp]
    public void SetUp()
    {
        _manager = new TestResourcesManager();
        _manager.Initialize();
        _sessionA = new FakeSession(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        _sessionB = new FakeSession(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }

    [Test]
    public void SetGetAndMissingResource()
    {
        Assert.That(_manager.TryGetResource(_sessionA, "credits", out _), Is.False);

        Assert.That(_manager.TrySetResource(_sessionA, "credits", 100), Is.True);
        Assert.That(_manager.TryGetResource(_sessionA, "credits", out var credits), Is.True);
        Assert.That(credits, Is.EqualTo(100));

        Assert.That(_manager.TryGetResource(_sessionA, "missing", out _), Is.False);
        Assert.That(_manager.TryGetResource(_sessionB, "credits", out _), Is.False);
    }

    [Test]
    public void ArbitraryResourceIdsAndBulkCopyOwnership()
    {
        Assert.That(_manager.TrySetResource(_sessionA, "tokens", 7), Is.True);
        Assert.That(_manager.TrySetResource(_sessionA, "credits", 3), Is.True);

        var inbound = new Dictionary<string, double>
        {
            ["tokens"] = 99,
            ["stars"] = 1,
        };

        Assert.That(_manager.TrySetResources(_sessionA, inbound), Is.True);
        inbound["tokens"] = -1; // mutating caller dict must not affect store

        Assert.That(_manager.TryGetResources(_sessionA, out var snapshot), Is.True);
        Assert.That(snapshot!["tokens"], Is.EqualTo(99));
        Assert.That(snapshot["stars"], Is.EqualTo(1));
        Assert.That(snapshot.ContainsKey("credits"), Is.False);

        snapshot["tokens"] = 0; // mutating returned snapshot must not affect store
        Assert.That(_manager.TryGetResource(_sessionA, "tokens", out var tokens), Is.True);
        Assert.That(tokens, Is.EqualTo(99));
    }

    [Test]
    public void UpdateDeltaAndSkipNullLinkFlag()
    {
        Assert.That(_manager.TrySetResource(_sessionA, "credits", 10, skipNullLink: true), Is.True);
        Assert.That(_manager.LastSkipNullLink, Is.True);

        Assert.That(_manager.TryUpdateResource(_sessionA, "credits", 5), Is.True);
        Assert.That(_manager.LastSkipNullLink, Is.False);
        Assert.That(_manager.TryGetResource(_sessionA, "credits", out var credits), Is.True);
        Assert.That(credits, Is.EqualTo(15));
        Assert.That(_manager.LastChangedId, Is.EqualTo("credits"));
        Assert.That(_manager.LastDiff, Is.EqualTo(5));
    }

    [Test]
    public void BulkReplaceDoesNotRaisePerResourceChange()
    {
        _manager.TrySetResource(_sessionA, "credits", 1);
        _manager.ChangeCount = 0;
        _manager.ReplaceCount = 0;

        _manager.TrySetResources(_sessionA, new Dictionary<string, double> { ["credits"] = 50, ["tokens"] = 2 });

        Assert.That(_manager.ChangeCount, Is.EqualTo(0));
        Assert.That(_manager.ReplaceCount, Is.EqualTo(1));
    }

    [Test]
    public void RemoveResourcesReturnsFinalSnapshotAndClears()
    {
        _manager.TrySetResource(_sessionA, "credits", 42);
        Assert.That(_manager.RemoveResources(_sessionA, out var final), Is.True);
        Assert.That(final!["credits"], Is.EqualTo(42));

        Assert.That(_manager.TryGetResources(_sessionA, out _), Is.False);
        Assert.That(_manager.RemoveResources(_sessionA, out _), Is.False);
    }

    [Test]
    public void SameValueSetIsNoOp()
    {
        Assert.That(_manager.TrySetResource(_sessionA, "credits", 5), Is.True);
        _manager.ChangeCount = 0;
        Assert.That(_manager.TrySetResource(_sessionA, "credits", 5), Is.False);
        Assert.That(_manager.ChangeCount, Is.EqualTo(0));
    }

    private sealed class TestResourcesManager : SharedNullLinkPlayerResourcesManager
    {
        public int ChangeCount;
        public int ReplaceCount;
        public bool LastSkipNullLink;
        public string? LastChangedId;
        public double LastDiff;

        public override void Initialize()
        {
            // Skip log manager dependency for unit tests.
        }

        protected override void OnResourceChanged(
            ICommonSession session,
            string id,
            double oldValue,
            double newValue,
            bool skipNullLink)
        {
            ChangeCount++;
            LastSkipNullLink = skipNullLink;
            LastChangedId = id;
            LastDiff = newValue - oldValue;
        }

        protected override void OnResourcesReplaced(ICommonSession session, Dictionary<string, double> resources)
        {
            ReplaceCount++;
        }
    }

    private sealed class FakeSession : ICommonSession
    {
        public FakeSession(Guid id)
        {
            UserId = new NetUserId(id);
            Name = id.ToString();
        }

        public SessionStatus Status { get; set; } = SessionStatus.InGame;
        public EntityUid? AttachedEntity { get; set; }
        public NetUserId UserId { get; }
        public string Name { get; set; }
        public short Ping { get; set; }
        public INetChannel Channel { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public LoginType AuthType => LoginType.Guest;
        public HashSet<EntityUid> ViewSubscriptions { get; } = [];
        public DateTime ConnectedTime { get; set; }
        public SessionState State => throw new NotSupportedException();
        public SessionData Data => throw new NotSupportedException();
        public bool ClientSide { get; set; }
    }
}
