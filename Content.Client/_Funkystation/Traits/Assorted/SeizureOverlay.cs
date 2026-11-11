// SPDX-FileCopyrightText: 2025 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared._Funkystation.Traits.Assorted;
using Robust.Shared.Timing;
using Robust.Shared.Enums;

namespace Content.Client._Funkystation.Traits.Assorted
{
    /// <summary>
    /// Seizure rendering extracted from the upstream overlay; migraines keep Starfall rendering.
    /// </summary>
    public sealed class SeizureOverlay : Overlay
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        public override bool RequestScreenTexture => true;
        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        private readonly ShaderInstance _cataractsShader;

        // Local state for fade-out when no component is present
        private bool _isFading = false;
        private float _localCurrentBlur = 0f;
        private float _localPulseAccumulator = 0f;
        private float _localPulseFrequency = 0.4f;
        private float _localPulseAmplitude = 0.8f;
        private float _localRampDownSpeed = 2f;
        private bool _localUseSoftShader = false;
        private float _localSoftness = 0.45f;

        public SeizureOverlay()
        {
            IoCManager.InjectDependencies(this);
            _cataractsShader = _prototypeManager.Index<ShaderPrototype>("Cataracts").InstanceUnique();
        }

        protected override bool BeforeDraw(in OverlayDrawArgs args)
        {
            if (!_entityManager.TryGetComponent(_playerManager.LocalSession?.AttachedEntity, out EyeComponent? eyeComp))
                return false;

            if (args.Viewport.Eye != eyeComp.Eye)
                return false;

            var playerEntity = _playerManager.LocalSession?.AttachedEntity;
            if (playerEntity == null)
                return false;

            // Check for SeizureOverlayComponent
            if (_entityManager.TryGetComponent<SeizureOverlayComponent>(playerEntity, out var seizure))
            {
                _isFading = seizure.IsFading;
                _localCurrentBlur = seizure.CurrentBlur;
                _localPulseAccumulator = seizure.PulseAccumulator;
                _localPulseFrequency = seizure.PulseFrequency;
                _localPulseAmplitude = seizure.PulseAmplitude;
                _localRampDownSpeed = seizure.RampDownSpeed > 0f ? seizure.RampDownSpeed : seizure.RampUpSpeed;
                _localUseSoftShader = seizure.UseSoftShader;
                _localSoftness = seizure.Softness;

                var pulseOffset = MathF.Sin(_localPulseAccumulator * _localPulseFrequency * MathF.PI * 2f) * _localPulseAmplitude;
                var strength = _localCurrentBlur + pulseOffset;
                if (_localUseSoftShader)
                    strength *= _localSoftness;

                return strength > 0f;
            }

            // Handle fade-out when neither component is present
            if (_localCurrentBlur > 0.001f || _localPulseAmplitude > 0.001f)
            {
                _isFading = true;
                return true;
            }

            return false;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (ScreenTexture == null)
                return;

            var playerEntity = _playerManager.LocalSession?.AttachedEntity;
            if (playerEntity == null)
                return;

            var viewport = args.WorldBounds;
            var frameTime = (float)_gameTiming.FrameTime.TotalSeconds;

            // Get zoom level for shader
            var zoom = 1.0f;
            if (_entityManager.TryGetComponent<EyeComponent>(playerEntity, out var eyeComponent))
            {
                zoom = eyeComponent.Zoom.X;
            }

            // Update local state based on active components
            if (_entityManager.TryGetComponent<SeizureOverlayComponent>(playerEntity, out var seizure))
            {
                _localPulseAccumulator = seizure.PulseAccumulator;
                _localCurrentBlur = seizure.CurrentBlur;
                _localPulseFrequency = seizure.PulseFrequency;
                _localPulseAmplitude = seizure.PulseAmplitude;
                _localUseSoftShader = seizure.UseSoftShader;
                _localSoftness = seizure.Softness;
                _localRampDownSpeed = seizure.RampDownSpeed > 0f ? seizure.RampDownSpeed : seizure.RampUpSpeed;
            }
            else
            {
                // Handle fade-out when no component is present
                _isFading = true;
                _localPulseAccumulator += frameTime;
                _localCurrentBlur = MathHelper.Lerp(_localCurrentBlur, 0f, frameTime * _localRampDownSpeed);
                _localPulseAmplitude = MathHelper.Lerp(_localPulseAmplitude, 0f, frameTime * _localRampDownSpeed);
            }

            // Calculate final effect strength
            var pulseOffset = MathF.Sin(_localPulseAccumulator * _localPulseFrequency * MathF.PI * 2f) * _localPulseAmplitude;
            var strength = _localCurrentBlur + pulseOffset;
            if (_localUseSoftShader)
                strength *= _localSoftness;

            strength = Math.Clamp(strength, 0f, BlurryVisionComponent.MaxMagnitude);
            var normalized = (float)Math.Pow(Math.Min(strength / BlurryVisionComponent.MaxMagnitude, 1.0f), BlurryVisionComponent.DefaultCorrectionPower);

            // Apply shader effects
            _cataractsShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            _cataractsShader.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
            _cataractsShader.SetParameter("Zoom", zoom);
            _cataractsShader.SetParameter("DistortionScalar", (float)Math.Pow(normalized, 2.0f));
            _cataractsShader.SetParameter("CloudinessScalar", (float)Math.Pow(normalized, 1.0f));

            var worldHandle = args.WorldHandle;
            worldHandle.UseShader(_cataractsShader);
            worldHandle.DrawRect(viewport, Color.White);
            worldHandle.UseShader(null);

            // Clean up fade state when complete
            if (_isFading && _localCurrentBlur <= 0.001f && _localPulseAmplitude <= 0.001f)
            {
                _isFading = false;
                _localPulseAccumulator = 0f;
                _localCurrentBlur = 0f;
                _localPulseAmplitude = 0f;
            }
        }
    }
}
