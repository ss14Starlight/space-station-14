// SPDX-FileCopyrightText: 2026 Mora <46364955+TrixxedHeart@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 TrixxedHeart <46364955+TrixxedBit@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared._Funkystation.Traits.Assorted;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Client._Funkystation.Traits.Assorted;

/// <summary>
/// Client system for seizure audio effects.
/// Plays sounds locally for the player experiencing the seizure.
/// </summary>
public sealed class SeizureSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _playedProdromeSound = false;
    private bool _playedSeizureSound = false;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeizureOverlayComponent, ComponentInit>(OnSeizureOverlayInit);
        SubscribeLocalEvent<SeizureOverlayComponent, ComponentShutdown>(OnSeizureOverlayShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var localEntity = _playerManager.LocalSession?.AttachedEntity;
        if (localEntity == null)
        {
            _playedProdromeSound = false;
            _playedSeizureSound = false;
            return;
        }

        // Check if local player has seizure overlay component
        if (TryComp<SeizureOverlayComponent>(localEntity.Value, out var overlay))
        {
            // Play prodrome sound during prodrome phase
            if (overlay.VisualState == SeizureVisualState.Prodrome && !_playedProdromeSound)
            {
                _audio.PlayPredicted(new SoundPathSpecifier("/Audio/_Funkystation/Effects/Migraine/prodrome.ogg"),
                    localEntity.Value, localEntity.Value);
                _playedProdromeSound = true;
            }
            // Play seizure sound during active seizure phase
            else if (overlay.VisualState == SeizureVisualState.Seizure && !_playedSeizureSound)
            {
                _audio.PlayPredicted(new SoundPathSpecifier("/Audio/_Funkystation/Effects/Migraine/neuroseize.ogg"),
                    localEntity.Value, localEntity.Value);
                _playedSeizureSound = true;
            }
        }
        else
        {
            // Reset flags when component is removed
            _playedProdromeSound = false;
            _playedSeizureSound = false;
        }
    }

    private void OnSeizureOverlayInit(EntityUid uid, SeizureOverlayComponent component, ComponentInit args)
    {
        // Reset sound flags when component is added
        var localEntity = _playerManager.LocalSession?.AttachedEntity;
        if (localEntity == uid)
        {
            _playedProdromeSound = false;
            _playedSeizureSound = false;
        }
    }

    private void OnSeizureOverlayShutdown(EntityUid uid, SeizureOverlayComponent component, ComponentShutdown args)
    {
        // Reset sound flags when component is removed
        var localEntity = _playerManager.LocalSession?.AttachedEntity;
        if (localEntity == uid)
        {
            _playedProdromeSound = false;
            _playedSeizureSound = false;
        }
    }
}
