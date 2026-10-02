using Content.Shared._Starlight.Temperature.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Clothing.EntitySystems;

/// <summary>
/// Adds examine text for clothing with temperature protection.
/// </summary>
public sealed class TemperatureProtectionExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<TemperatureProtectionComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<TemperatureProtectionComponent> ent, ref ExaminedEvent args)
    {
        // some entites, like Vulpkanin, use TemperatureProtection components and we don't want to add this examine text to them
        if(!HasComp<ClothingComponent>(ent)) return;

        var component = ent.Comp;
        var examimeMarkup = GetTemperatureExamine(component);

        args.PushMarkup(examimeMarkup.ToMarkup());
    }

    /// <summary>
    /// Creatures some nice formatted markup for our temperature protection.
    /// </summary>
    private FormattedMessage GetTemperatureExamine(TemperatureProtectionComponent component)
    {
        // Format shamelessly copied from SharedArmorSystem.cs
        var msg = new FormattedMessage();

        // cold protection
        if (component.CoolingCoefficient != 1f)
        {
            var temperatureColor = "lightblue";
            var temperatureType = Loc.GetString("clothing-temperature-type-cold");

            // show temperature coefficients as % reductions
            var reduceOrIncreaseText = component.CoolingCoefficient < 1f ? "clothing-temperature-protection" : "clothing-temperature-vulnerable";
            var temperatureValue = GetRelativePercentage(component.CoolingCoefficient);

            msg.AddMarkupOrThrow(Loc.GetString(reduceOrIncreaseText,
                    ("color", temperatureColor),
                    ("type", temperatureType),
                    ("value", temperatureValue)
            ));
        }

        // heat protection
        if (component.HeatingCoefficient != 1f)
        {
            // new line if we printed cold protection stuff
            if (component.CoolingCoefficient != 1f) msg.PushNewline();

            var temperatureColor = "orange";
            var temperatureType = Loc.GetString("clothing-temperature-type-hot");

            // show temperature coefficients as % reductions
            var reduceOrIncreaseText = component.HeatingCoefficient < 1f ? "clothing-temperature-protection" : "clothing-temperature-vulnerable";
            var temperatureValue = GetRelativePercentage(component.HeatingCoefficient);

            msg.AddMarkupOrThrow(Loc.GetString(reduceOrIncreaseText,
                    ("color", temperatureColor),
                    ("type", temperatureType),
                    ("value", temperatureValue)
            ));
        }

        return msg;
    }

    // using string formatting because Math.Round had issues with the tiny coefficients (0.0001) for some hardsuits
    private string GetRelativePercentage(float coefficient) =>
        coefficient < 1f ? ((1f - coefficient) * 100).ToString("0.#") : ((coefficient - 1f) * 100).ToString("0.#");
}
