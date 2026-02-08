using Content.Shared._Starlight.Magic.Components;
using Content.Shared._Starlight.Magic.Events;
using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Magic.Systems;

public sealed class ColorObjectToEyeColorSystem : EntitySystem
{
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColorObjectToEyeColorComponent, AfterSpawnItemInHandEvent>(OnAfterSpawnItemInHand);
    }
    
    private void OnAfterSpawnItemInHand(Entity<ColorObjectToEyeColorComponent> entity, ref AfterSpawnItemInHandEvent ev)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ev.Performer, out var appearanceComp))
            return;
        
        var color = NormalizeColor(appearanceComp.EyeColor, 1.8f);

        _pointLight.SetColor(ev.Entity, color);

        _appearance.SetData(ev.Entity, ColorVisuals.Color, color);
    }
    
    /// <summary>
    ///     Normalize the given color and ensure its total brightness matches <see cref="totalBrightness"/>
    /// </summary>
    /// <param name="color">Color to normalize</param>
    /// <param name="totalBrightness">The targeted sum of all RGB values.</param>
    /// <returns></returns>
    private Color NormalizeColor(Color color, float totalBrightness)
    {
        var power = color.R + color.G + color.B;

        if (power == 0f)
            return new Color(totalBrightness/3.0f, totalBrightness/3.0f, totalBrightness/3.0f);

        var scale = totalBrightness / power;

        float[] newColors = [ color.R * scale, color.G * scale, color.B * scale ];

        // The will overflow, collect the residues and we will put them back in later.
        var reminder = 0.0f;
        var overweight = 0;
        for (var i=0; i<newColors.Length; i++)
        {
            if (newColors[i] <= 1)
                continue;
            
            reminder += newColors[i] % 1.0f;
            newColors[i] = 1.0f;
            overweight++;
        }
        
        for (var i=0; i<newColors.Length; i++)
        {
            if (newColors[i] > .99)
                continue;
            
            newColors[i] += reminder/(newColors.Length-overweight);
        }

        return new  Color(newColors[0], newColors[1], newColors[2]);
    }
    
}

[Serializable, NetSerializable]
public enum ColorVisuals : byte
{
    Color,
}
