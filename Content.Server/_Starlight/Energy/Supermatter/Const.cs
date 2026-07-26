using Content.Shared.Atmos;
using Content.Shared.FixedPoint;

namespace Content.Server._Starlight.Energy.Supermatter;

internal static class Const
{
    public static FixedPoint2 HeatPercent = 0.82f;
    public static FixedPoint2 BreakPercent = 0.04f;
    public static FixedPoint2 LightingPercent = 0.03f;
    public static FixedPoint2 RadiationPercent = 0.11f;

    public static FixedPoint2 DamageMultiplier = 3.14f;//Typo or i dont know what DamageMultiplayer is

    public static float MinimumSpecialGasMoles = 3f;

    public static GasProperties[] GasProperties =
    [//  HEATT   HEATM  RADS REGEM REACM DESM GADA
        new (0.24f, 1.20f, 1.4f, 0f, 0f, 0f, 0f), // oxygen
        new (0.20f, 1.46f, 1.5f, 0f, 0f, 0f, 0f), // nitrogen
        new (0.12f, 2.21f, 3.5f, 0f, 0f, 0f, 0f), // carbon dioxide
        new (0.60f, 0.61f, 1.3f, 0f, 0f, 0f, 0f), // plasma
        new (0.30f, 0.45f, 1.2f, 0f, 0.5f, 0f, 0f), // tritium
        new (0.14f, 2.31f, 2.5f, 0f, 0f, 0f, 0f), // vapor
        new (0.16f, 2.11f, 4.4f, 0f, 0f, 0f, 0f), // ammonia
        new (0.13f, 2.19f, 2.2f, 0f, -0.02f, 0f, 0f), // nitrous oxide
        new (1.00f, 0.01f, 1.1f, 0f, -0.1f, 0f, 0f), // frezon
        new (0.25f, 1.20f, 1.80f, 0f, 0f, 0f, 0f), // BZ (catalist so an not competing gas) //FUNKY GASES
        new (0.40f, 0.50f, 1.8f, 1f, -0.1f, -0.1f, 0f), // Healium (support gas) {Special effect: increase passive}
        new (0.30f, 0.50f, 1.2f, 0f, 0.75f, 0.5f, 0f), // Nitrium {Special effect: SM reacts more, cost of stability}
        new (0.30f, 1.0f, 1.7f, 0f, 0f, 0f, 0f), // Pluoxium better then o2 not insane
        new (0.35f, 0.35f, 1.1f, 0f, 0.25f, 0f, 0f), // Hydrogen Better heatT than Trit but "ignites" faster
        new (2.0f, 0.05f, 1.05f, 0f, -0.5f, -0.5f, 0f), // HyperNoblium Hard to make thus great heat absorb its an supercooland, decreases reacfifiy
        new (0.20f, 1.5f, 1.5f, 0f, 2.0f, 0.5f, 0f), // ProtoNitrate (heavy gas/cloning gas) 2 might be an bit much
        new (0.10f, 3.00f, 1.01f, -0.5f, 0f, 0.5f, 10f), // Zauker (Heats up quickly, why would you get this near the SM?) {Special effect:decrease passive + moddamage}
        new (0.1f, 3.0f, 3.0f, 0f, -1f, 0f, 0f), // Halon (horrible cooland, but great rad protection, flooding SM makes it stop fire anyways.)
        new (0.45f, 0.4f, 1.3f, 0f, -0.1f, 0f, 0f), // Helium (good thermal conductivity, heats quickly to molar mass)
        new (0.10f, 4.0f, 1.1f, -0.2f, 1.0f, 0.5f, 0f), // AntiNoblium (HyNoB stable, thus "anti" highly unstable) //END FUNKY
        new (0.25f, 1.2f, 1.7f, 0f, 0f, 0f, 0f), // Ulnitranium (close to ploux) //STARLIGHT GASES
        new (0.35f, 1.0f, 2.0f, 0.2f, -0.2f, 0f, 0f), // ZXA (another support gas) //END STARLIGHT
    ];

    /*Cooling gases: Frezon, HyperNob
    Stable gases: Pluox, Ulnitranium, ZXA
    Support gases: BZ, Healium
    Power/risk gases: Tritium, Hydrogen, Nitrium
    Sabotage gases: Zauker, AntiNob
    Fire-control gas: Halon //halon kills fire already so blocking rads is the #1 countermeasure missing.
    */

    public static float MinPressure = 33f;
    public static float MaxPressure = 363.9f;

    public static float MaxTemperature = Atmospherics.T0C + 200;

    public static float EvaporationCompensation = 10;

    public static float MinRadiationStability = 1.1f;
    // Minimum radiation decay divisor.
    // Higher values = radiation disappears faster.
    // Prevents gases from making radiation effectively permanent.

    public static float MaxRadiationStability = 10f;
    // Maximum radiation decay divisor.
    // Prevents gases like Halon from making radiation disappear instantly.

    public static float MinReactionModifier = 0.1f;
    // Minimum global Supermatter reaction multiplier.
    // 1.0 = normal reaction speed.
    // Lower values reduce heat, radiation, lighting and gas production.
    // Prevents gases from completely stopping Supermatter reactions.

    public static float MaxReactionModifier = 3f;
    // Maximum global Supermatter reaction multiplier.
    // Allows gases like Nitrium/ProtoNitrate to increase activity,
    // but prevents exponential runaway behaviour.

    public static float MinRegenerationModifier = 0f;
    // Minimum passive regeneration multiplier.
    // 0 = no passive healing.
    // Prevents negative regeneration (additional damage).

    public static float MaxRegenerationModifier = 5f;
    // Maximum passive regeneration multiplier.
    // Limits healing gases like Healium from making the crystal immortal.

    public static float MinDestabilizationModifier = 0.1f;
    // Minimum damage accumulation multiplier for breaking.
    // Multiplier for durability loss from accumulated damage.
    // 1.0 = normal.
    // <1.0 = stabilization.
    // >1.0 = faster delamination.
    // Prevents gases from completely stopping durability loss.

    public static float MaxDestabilizationModifier = 3f;
    // Maximum damage accumulation multiplier for breaking.
    // Allows destabilizing gases to accelerate delamination,
    // but prevents instant destruction.

    public static FixedPoint2 MaxDamagePerSecond = (100f / 180f) + RegenerationPerSecond; // Ensures it takes at least 3 minutes to deplete
    public static FixedPoint2 RegenerationPerSecond = 0.3f;

    public static string[] AudioCrack = ["/Audio/_Starlight/Effects/supermatter/crystal_crack_1.ogg", "/Audio/_Starlight/Effects/supermatter/crystal_crack_2.ogg"];
    public static string[] AudioBurn = ["/Audio/_Starlight/Effects/supermatter/burning_1.ogg", "/Audio/_Starlight/Effects/supermatter/burning_2.ogg", "/Audio/_Starlight/Effects/supermatter/burning_3.ogg"];
    public static string AudioEvaporate = "/Audio/_Starlight/Effects/supermatter/emitter2.ogg";
}
public record struct GasProperties(float HeatTransferPerMole, float HeatModifier, float RadiationStability, float RegenerationModifier, float ReactionModifier, float DestabilizationModifier, float GasDamage);

