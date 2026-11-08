using Content.Shared._Starlight.Light.BackupLight;
using Content.Shared._Starlight.Light.NightLighting;
using Content.Shared._Starlight.Shadekin.Components;
using Content.Shared.Light.Components;
using Content.Shared.Power.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    /// <summary>
    /// Lights a fixture that lost power from its backup cell, see <see cref="BackupLightComponent"/>.
    /// </summary>
    /// <returns>Whether the fixture is now running from its cell.</returns>
    private bool TrySetBackupLight(EntityUid uid,
        PoweredLightComponent light,
        Entity<LightBulbComponent> bulb,
        SharedApcPowerReceiverComponent powerReceiver,
        AppearanceComponent? appearance)
    {
        if (!TryComp<BackupLightComponent>(uid, out var backup))
            return false;

        if (powerReceiver.Powered || !light.On || !backup.Charged)
        {
            _appearance.SetData(uid, BackupLightVisuals.Active, false, appearance);
            return false;
        }

        var radius = bulb.Comp.LightRadius * backup.RadiusMultiplier;
        var energy = bulb.Comp.LightEnergy * backup.EnergyMultiplier;

        if (TryComp<NightLightingDimmedComponent>(uid, out var night))
        {
            radius *= night.RadiusMultiplier;
            energy *= night.EnergyMultiplier;
        }

        SetLight(uid,
            true,
            backup.Color,
            light,
            radius,
            energy,
            bulb.Comp.LightSoftness,
            HasComp<DarkLightComponent>(bulb));

        _appearance.SetData(uid, PoweredLightVisuals.BulbState, PoweredLightState.On, appearance);
        // Also gets the visualizer to pick up the new glow colour, the bulb state alone may not have changed.
        _appearance.SetData(uid, BackupLightVisuals.Active, true, appearance);
        return true;
    }

    /// <summary>
    /// Power the fixture draws on top of its bulb to charge its backup cell.
    /// </summary>
    public float GetBackupLoad(EntityUid uid)
        => TryComp<BackupLightComponent>(uid, out var backup) && backup.Charging ? backup.RechargeRate : 0f;

    private void ClearBackupLight(EntityUid uid, AppearanceComponent? appearance)
    {
        if (HasComp<BackupLightComponent>(uid))
            _appearance.SetData(uid, BackupLightVisuals.Active, false, appearance);
    }
}
