using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Atmos.Reactions;

[UsedImplicitly]
public sealed partial class ProtoNitratePlasmaReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
    ///I have no clue what im doing, if you dont like it and can code this better, do so.

    ///Getting PN and all gasses it can decompose (except for BZ)

        var initProtoNitrate = mixture.GetMoles(Gas.ProtoNitrate);
        var initTritium = mixture.GetMoles(Gas.Tritium);
        var initFrezon = mixture.GetMoles(Gas.Frezon);
        var initHealium = mixture.GetMoles(Gas.Healium);
        var initNitrium = mixture.GetMoles(Gas.Nitrium);
        var initHyperNoblium = mixture.GetMoles(Gas.HyperNoblium);
        var initAntiNoblium = mixture.GetMoles(Gas.AntiNoblium);
        var initHalon = mixture.GetMoles(Gas.Halon);
        var initZauker = mixture.GetMoles(Gas.Zauker);
        var initZXA = mixture.GetMoles(Gas.ZXA);

    ///Getting physical constants that constrain the reaction

        var pressure = mixture.Pressure;
        var temperature = mixture.Temperature;

    ///Determine reaction rate based on physical constants and PN present. Temperature increases rate. Pressure decreases speed similar to BZ, less decrease from pressure when more PN.

        var rate = (.25f * temperature / ( (pressure/initProtoNitrate) ));

    ///Check presence each gas that can decompose, if greater than 1, decompose equal to rate and add decompose value to production.

        var decomposeTrit = (0f);
        var decomposeFrez = (0f);
        var decomposeHeal = (0f);
        var decomposeNitr = (0f);
        var decomposeHNob = (0f);
        var decomposeANob = (0f);
        var decomposeHalon = (0f);
        var decomposeZauker = (0f);
        var decomposeZXA = (0f);

        if (initTritium > 1f)
        {
        decomposeTrit = (rate); ///Basic and inefficient method of turning mundane gas into plasma if using oxy, pluox method only way to make co2 into plasma.
        }
        if (initFrezon > 1f)
        {
        decomposeFrez = (rate); ///Assuming efficient frezon production, more efficient than trit but only uses oxy.
        }
        if (initHealium > 1f)
        {
            decomposeHeal = (rate); ///Higher complexity means higher efficiency.
        }
        if (initNitrium > 1f)
        {
            decomposeNitr = (rate); ///Further jump in efficiency, use this boon of fuel to make more nitrium.
        }
        if (initHyperNoblium > 1f)
        {
            decomposeHNob = (rate); ///Low efficiency due to the bz catalyst effect allowing for potential mass production alongside frezons nitrogen production capability.
        }
        if (initAntiNoblium > 1f)
        {
            decomposeANob = (rate); ///Very high efficiency so that anti-nob has an actual use.
        }
        if (initHalon > 1f)
        {
            decomposeHalon = (rate); ///One of two ways to make BZ into plasma, and the one not reliant on water.
        }
        if (initZauker > 1f)
        {
            decomposeZauker = (rate); ///Zonker.
        }
        if (initZXA > 1f)
        {
            decomposeZXA = (rate); ///Allows you to turn water vapor into plasma.
        }

        var production = ( (decomposeTrit * 1.25f) + (decomposeFrez * 0.03f) + (decomposeHeal * .1f) + (decomposeNitr * 5f) + (decomposeHNob * .2f) + (decomposeANob * 1f) + (decomposeHalon * .5f) + (decomposeZauker * 15f) + (decomposeZXA * .5f) );

        if (production < 0.1f)
            return ReactionResult.NoReaction;

///One PN becomes 20 plasma. More different types of gas in the soup can improve the rate of production without upgrading the mixer.	Massive helium byproduct to fuck up your pressure.

        mixture.AdjustMoles(Gas.ProtoNitrate, production * -0.05f);
        mixture.AdjustMoles(Gas.Plasma, production);
        mixture.AdjustMoles(Gas.Helium, production * (100f/pressure));
        mixture.AdjustMoles(Gas.Tritium, -decomposeTrit);
        mixture.AdjustMoles(Gas.Frezon, -decomposeFrez);
        mixture.AdjustMoles(Gas.Healium, -decomposeHeal);
        mixture.AdjustMoles(Gas.Nitrium, -decomposeNitr);
        mixture.AdjustMoles(Gas.HyperNoblium, -decomposeHNob);
        mixture.AdjustMoles(Gas.AntiNoblium, -decomposeANob);
        mixture.AdjustMoles(Gas.Halon, -decomposeHalon);
        mixture.AdjustMoles(Gas.Zauker, -decomposeZauker);
        mixture.AdjustMoles(Gas.ZXA, -decomposeZXA);

        var energyReleased = (Atmospherics.ProtoNitrateBZConversionEnergy * (production + (production * 100f/pressure)));

        var heatCap = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (heatCap > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = Math.Max((mixture.Temperature * heatCap + energyReleased) / heatCap, Atmospherics.TCMB);

        return ReactionResult.Reacting;
    }
}
