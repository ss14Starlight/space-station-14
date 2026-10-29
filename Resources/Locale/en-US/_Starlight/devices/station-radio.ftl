signal-port-name-vinyl-player = Vinyl player output
signal-port-description-vinyl-player = The vinyl player's music output. Connect to the rig.

signal-port-name-cd-player = Radio CD player output
signal-port-description-cd-player = The radio CD player's output. Connect to the rig, it plays alongside the vinyl player.

signal-port-name-radio-mixer = Radio mixer output
signal-port-description-radio-mixer = The radio mixer's control output. Connect to the rig that feeds the server it should control.

signal-port-name-radio-rig = Radio rig input
signal-port-description-radio-rig = The input of a radio rig, connect a vinyl player to this and then connect the rig to the radio server.

signal-port-name-radio-server = Server input
signal-port-description-radio-server = The input of the radio server, connect a radio rig to this to play music around the station!

vinyl-popout-no-station = The vinyl ejects itself, you're not on a station!
vinyl-popout-no-power = The vinyl ejects itself, the player isn't powered!
vinyl-popout-no-radio-connection = The vinyl ejects itself, the player isn't connected to the radio system!

# Starlight - Station Radio Examination Text.
vinyl-player-examine-empty = It's empty.
vinyl-player-examine-loaded = There's a copy of { $vinyl } inside.
vinyl-player-examine-playing = It's playing.
vinyl-player-examine-paused = It's paused.
vinyl-player-verb-play = Play
vinyl-player-verb-pause = Pause
vinyl-player-verb-rewind = Rewind
vinyl = vinyl
station-radio-server-cancelled-anchor = This station already has a radio server!

station-radio-receiver-examine-full-volume = It's playing at full volume.
station-radio-receiver-examine-low-volume = It's playing.
ui-options-radio-music-volume = Radio music volume:

station-radio-server-examine-recording = The station server is not recording.
station-radio-server-examine-not-recording = The station server is currently recording.

station-radio-server-microphone-on-use = The microphone is { $radioState }.
# Starlight - End

item-slot-component-slot-name-vinyl = vinyl
item-slot-component-slot-name-cd = CD

station-radio-mixer-title = Radio mixer
station-radio-mixer-no-server = Not linked to a powered radio server. Link the mixer to the radio rig.
station-radio-mixer-voice = Voice
station-radio-mixer-master = Master
station-radio-mixer-monitor = Monitor
station-radio-mixer-level = {$level}%
station-radio-mixer-empty = no disc
station-radio-mixer-playlist = Playlist
station-radio-mixer-next = Next
station-radio-mixer-eject = Eject
station-radio-mixer-tracks = { $name } tracks
station-radio-mixer-track = { $number }. { $disc }

signal-port-name-broadcast-recorder = Radio recorder output
signal-port-description-broadcast-recorder = The radio recorder's link. Connect to the rig to record and play back that server's broadcast.
item-slot-component-slot-name-broadcast-tape = broadcast tape

broadcast-recorder-title = Radio recorder
broadcast-recorder-no-tape = No tape
broadcast-recorder-no-server = Not linked to a powered radio server. Link the radio recorder to the radio rig.
broadcast-recorder-mode-stopped = Stopped
broadcast-recorder-mode-recording = Recording
broadcast-recorder-mode-playing = Playing
broadcast-recorder-record = Record
broadcast-recorder-play = Play
broadcast-recorder-stop = Stop
broadcast-recorder-rewind = Rewind
broadcast-recorder-popup-no-tape = There is no tape in the radio recorder.
broadcast-recorder-popup-no-power = The recorder has no power.
broadcast-recorder-popup-no-server = The radio recorder isn't linked to a powered radio server.
broadcast-recorder-popup-tape-full = The tape is full.
broadcast-tape-examine = It has [color=yellow]{ $recorded }[/color] of [color=yellow]{ $max }[/color] recorded.

wire-name-station-radio-music = MUSC
wire-name-station-radio-microphone = MIC
wire-name-station-radio-interference = NOIS

radio-advert-popup-paid = The advert earned { $reward } spesos from { $listeners } { $listeners ->
    [one] listener
   *[other] listeners
} ({ $quality }% clean airtime).
radio-advert-popup-repeat = The listeners heard this advert not long ago, it earns nothing.
radio-advert-popup-no-listeners = Nobody was listening to the advert.
radio-advert-popup-too-soon = Listeners are tired of adverts, wait { $minutes } more { $minutes ->
    [one] minute
   *[other] minutes
} before the next one pays.
radio-advert-popup-drowned = The advert was drowned out by music or talk, it earned nothing.

station-radio-mixer-listeners = Listeners: { $count }
station-radio-mixer-interference = Interference on air!

station-radio-receiver-title = Station radio
station-radio-receiver-power = Power
station-radio-receiver-knob = Volume: drag or scroll

record-crate-full = The crate is full.
record-crate-examine = It holds [color=yellow]{ $count }[/color] of { $capacity } records.
