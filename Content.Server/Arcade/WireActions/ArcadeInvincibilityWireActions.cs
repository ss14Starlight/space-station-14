using Content.Server.Arcade.SpaceVillain;
using Content.Server._Starlight.Arcade.Lancer;
using Content.Server.Wires;
using Content.Shared.Arcade.SpaceVillain;
using Content.Shared.Wires;

namespace Content.Server.Arcade;

public sealed partial class ArcadePlayerInvincibleWireAction : BaseToggleWireAction
{
    public override string Name { get; set; } = "wire-name-arcade-invincible";

    public override Color Color { get; set; } = Color.Purple;

    public override object? StatusKey { get; } = SpaceVillainIndicators.HealthManager;

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        // Cut → setting=false → Invincible=true; Mend → setting=true → Invincible=false.
        if (EntityManager.TryGetComponent<SpaceVillainArcadeComponent>(owner, out var spaceVillain)
            && spaceVillain.Game != null)
        {
            spaceVillain.Game.PlayerChar.Invincible = !setting;
        }

        if (EntityManager.TryGetComponent<LancerArcadeComponent>(owner, out var lancer))
            lancer.PlayerInvincible = !setting;
    }

    public override bool GetValue(EntityUid owner)
    {
        if (EntityManager.TryGetComponent<SpaceVillainArcadeComponent>(owner, out var spaceVillain)
            && spaceVillain.Game != null)
            return !spaceVillain.Game.PlayerChar.Invincible;

        if (EntityManager.TryGetComponent<LancerArcadeComponent>(owner, out var lancer))
            return !lancer.PlayerInvincible;

        return true;
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (EntityManager.TryGetComponent<SpaceVillainArcadeComponent>(wire.Owner, out var spaceVillain)
            && spaceVillain.Game != null)
        {
            return spaceVillain.Game.PlayerChar.Invincible || spaceVillain.Game.VillainChar.Invincible
                ? StatusLightState.BlinkingSlow
                : StatusLightState.On;
        }

        if (EntityManager.TryGetComponent<LancerArcadeComponent>(wire.Owner, out var lancer))
        {
            return lancer.PlayerInvincible
                ? StatusLightState.BlinkingSlow
                : StatusLightState.On;
        }

        return StatusLightState.Off;
    }
}

public sealed partial class ArcadeEnemyInvincibleWireAction : BaseToggleWireAction
{
    public override string Name { get; set; } = "wire-name-player-invincible";
    public override Color Color { get; set; } = Color.Purple;

    public override object? StatusKey { get; } = null;

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        if (EntityManager.TryGetComponent<SpaceVillainArcadeComponent>(owner, out var arcade)
            && arcade.Game != null)
        {
            arcade.Game.VillainChar.Invincible = !setting;
        }
    }

    public override bool GetValue(EntityUid owner)
    {
        return EntityManager.TryGetComponent<SpaceVillainArcadeComponent>(owner, out var arcade)
            && arcade.Game != null
            && !arcade.Game.VillainChar.Invincible;
    }

    public override StatusLightData? GetStatusLightData(Wire wire)
    {
        return null;
    }
}

public enum ArcadeInvincibilityWireActionKeys : short
{
    Player,
    Enemy
}
