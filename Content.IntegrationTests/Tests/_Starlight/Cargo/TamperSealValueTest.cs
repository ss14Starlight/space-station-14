using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Shared._Starlight.Cargo.TamperSeal;
using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Starlight.Cargo;

[TestOf(typeof(SharedTamperSealValueSystem))]
public sealed class TamperSealValueTest : GameTest
{
    private const string StationProto = "SLTamperSealTestStation";
    private const string CrateProto = "CrateGenericSteel";
    private const string SturdyProto = "SLTamperSealTestSturdyBox";
    private const int StartingBalance = 1000;
    private const int Penalty = 200;
    private const int Refund = 150;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: SLTamperSealTestStation
  parent: [ BaseStation, BaseStationCargo ]

# Breaks the seal at its destruction threshold but survives, so it can be hit again.
- type: entity
  id: SLTamperSealTestSturdyBox
  components:
  - type: Appearance
  - type: Damageable
    damageContainer: Inorganic
  - type: Destructible
    thresholds:
    - trigger: !type:DamageTrigger
        damage: 100
      behaviors:
      - !type:DoActsBehavior
        acts: [ ""Breakage"" ]
";
    private const string DamageId = "Blunt";

    private async Task<(EntityUid Station, EntityUid Crate)> SpawnSealedCrate(string deliverer, string recipient, string proto = CrateProto)
    {
        var map = await Pair.CreateTestMap();
        EntityUid station = default;
        EntityUid crate = default;

        await Server.WaitPost(() =>
        {
            station = SEntMan.SpawnEntity(StationProto, MapCoordinates.Nullspace);
            var bank = SEntMan.GetComponent<StationBankAccountComponent>(station);
            var cargo = Server.System<CargoSystem>();
            foreach (var (account, balance) in bank.Accounts.ToList())
                cargo.UpdateBankAccount((station, bank), StartingBalance - balance, account);

            crate = SEntMan.SpawnEntity(proto, map.GridCoords);

            var seal = SEntMan.EnsureComponent<TamperSealComponent>(crate);
            seal.Deliverer = deliverer;
            seal.Recipient = recipient;

            var value = SEntMan.EnsureComponent<TamperSealValueComponent>(crate);
            value.StationId = station;
            value.Penalty = Penalty;
            value.Refund = Refund;
        });

        return (station, crate);
    }

    /// <returns>Whether the damage was actually applied.</returns>
    private async Task<bool> Smash(EntityUid crate, int amount = 100000)
    {
        var applied = false;
        await Server.WaitPost(() =>
        {
            var damage = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>(DamageId), FixedPoint2.New(amount));
            applied = Server.System<DamageableSystem>().TryChangeDamage(crate, damage, ignoreResistances: true);
        });
        await RunTicksSync(5);
        return applied;
    }

    private int Balance(EntityUid station, string account)
        => SEntMan.GetComponent<StationBankAccountComponent>(station).Accounts[account];

    [Test]
    public async Task DestroyingSealPenalisesDelivererAndRefundsRecipient()
    {
        var (station, crate) = await SpawnSealedCrate("Cargo", "Engineering");

        await Smash(crate);

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Balance(station, "Cargo"), Is.EqualTo(StartingBalance - Penalty - Refund));
                Assert.That(Balance(station, "Engineering"), Is.EqualTo(StartingBalance + Refund));
            }
        });
    }

    [Test]
    public async Task SealIsOnlyPenalisedOnce()
    {
        var (station, box) = await SpawnSealedCrate("Cargo", "Engineering", SturdyProto);
        const int Penalised = StartingBalance - Penalty - Refund;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Smash(box, 150), "The first hit did no damage.");
            Assert.That(SEntMan.EntityExists(box), "The box must survive the first hit for the second one to mean anything.");
            Assert.That(Balance(station, "Cargo"), Is.EqualTo(Penalised), "Breaking the seal did not penalise the deliverer.");

            Assert.That(await Smash(box, 150), "The second hit did no damage.");

            Assert.That(Balance(station, "Cargo"), Is.EqualTo(Penalised), "The seal was penalised more than once.");
            Assert.That(Balance(station, "Engineering"), Is.EqualTo(StartingBalance + Refund), "The refund was paid more than once.");
        }
    }

    [Test]
    public async Task SelfAddressedSealMovesNoMoney()
    {
        var (station, crate) = await SpawnSealedCrate("Cargo", "Cargo");

        await Smash(crate);

        await Server.WaitAssertion(() => Assert.That(Balance(station, "Cargo"), Is.EqualTo(StartingBalance)));
    }
}
