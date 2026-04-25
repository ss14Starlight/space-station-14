using Content.Server.Atmos.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Examine;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Radio.Components;
using Content.Shared.Radio;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Radio.EntitySystems
{
    public sealed class TelecomOverheatSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
        [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
        [Dependency] private readonly TransformSystem _transformSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        private const float HeatPerTilePerSecond = 300f;
        private const float OverheatTemperature = Atmospherics.FireMinimumTemperatureToExist + 25f;
        private const float CooldownTemperature = Atmospherics.T20C + 10f;

        private static readonly Vector2i[] NearbyOffsets =
        {
            new(0, 0),
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
            new(1, 1),
            new(-1, 1),
            new(1, -1),
            new(-1, -1)
        };
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<TelecomServerComponent, ExaminedEvent>(OnExaminedEvent);
        }
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<TelecomServerComponent, EncryptionKeyHolderComponent, ApcPowerReceiverComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var telecom, out var keys, out var power, out var xform))
            {
                if (telecom.Overheated)
                {
                    TryResetOverheat(uid, telecom, power, xform);
                    continue;
                }

                var indices = _transformSystem.GetGridTilePositionOrDefault((uid, xform));
                var grid = xform.GridUid;
                var map = xform.MapUid;
                var mixture = _atmosphere.GetTileMixture(grid, map, indices, excite: true);
                if (mixture == null || mixture.TotalMoles <= 0)
                {
                    telecom.SpacedDisabled = true;
                    _power.SetPowerDisabled(uid, true, power);
                    continue;
                }

                if (telecom.SpacedDisabled)
                {
                    telecom.SpacedDisabled = false;
                    if (!telecom.Overheated)
                    {
                        _power.SetPowerDisabled(uid, false, power);
                    }
                }

                if (power.PowerDisabled || !_power.IsPowered(uid) || !ServerHasActiveStationChannel(keys))
                {
                    continue;
                }

                var maxTemperature = HeatNearbyGas(uid, xform, frameTime);
                if (maxTemperature >= OverheatTemperature)
                {
                    telecom.Overheated = true;
                    _appearance.SetData(uid, TelecomServerVisuals.Overheated, true);
                    _power.SetPowerDisabled(uid, true, power);
                }
            }
        }

        private bool ServerHasActiveStationChannel(EncryptionKeyHolderComponent keys)
        {
            if (keys.CustomChannels.Count > 0)
            {
                return true;
            }

            foreach (var channel in keys.Channels)
            {
                if (_prototypeManager.TryIndex(channel, out RadioChannelPrototype? proto))
                {
                    if (!proto.LongRange)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        private void TryResetOverheat(EntityUid uid, TelecomServerComponent telecom, ApcPowerReceiverComponent power, TransformComponent xform)
        {
            var indices = _transformSystem.GetGridTilePositionOrDefault((uid, xform));
            var grid = xform.GridUid;
            var map = xform.MapUid;
            var highestTemperature = float.MinValue;
            var foundMixture = false;

            foreach (var offset in NearbyOffsets)
            {
                var mixture = _atmosphere.GetTileMixture(grid, map, indices + offset, excite: true);
                if (mixture == null)
                {
                    continue;
                }

                foundMixture = true;
                highestTemperature = MathF.Max(highestTemperature, mixture.Temperature);
            }

            if (!foundMixture)
            {
                return;
            }

            if (highestTemperature <= CooldownTemperature)
            {
                telecom.Overheated = false;
                _appearance.SetData(uid, TelecomServerVisuals.Overheated, false);
                _power.SetPowerDisabled(uid, false, power);
            }
        }

        private float HeatNearbyGas(EntityUid uid, TransformComponent xform, float frameTime)
        {
            var indices = _transformSystem.GetGridTilePositionOrDefault((uid, xform));
            var grid = xform.GridUid;
            var map = xform.MapUid;
            var maxTemperature = float.MinValue;
            var heatPerTile = HeatPerTilePerSecond * frameTime;

            foreach (var offset in NearbyOffsets)
            {
                var mixture = _atmosphere.GetTileMixture(grid, map, indices + offset, excite: true);
                if (mixture == null)
                {
                    continue;
                }

                _atmosphere.AddHeat(mixture, heatPerTile);
                maxTemperature = MathF.Max(maxTemperature, mixture.Temperature);
            }

            return maxTemperature;
        
    
}

        private void OnExaminedEvent(EntityUid uid, TelecomServerComponent component, ExaminedEvent args)
        {
            var xform = Transform(uid);
            var indices = _transformSystem.GetGridTilePositionOrDefault((uid, xform));
            var grid = xform.GridUid;
            var map = xform.MapUid;
            var serverTemperature = 0f;

            var mixture = _atmosphere.GetTileMixture(grid, map, indices, excite: true);
            if (mixture == null || mixture.TotalMoles <= 0)
            {
                args.PushMarkup(Loc.GetString("telecom-spaced"));
                return;
            }

            serverTemperature = mixture.Temperature;

            if (component.Overheated)
            {   
                args.PushMarkup(Loc.GetString("telecom-overheated"));
            }

            if (Loc.TryGetString("telecom-server-examined",
                out var str,
                ("tempColor", "red"),
                ("currenttemp", Math.Round(serverTemperature, 2))))
            {
                args.PushMarkup(str);
            }
        }
    }
}