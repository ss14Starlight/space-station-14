blade-server-rack-window-title = Blade Server Rack
blade-server-rack-window-footer-flavor = DEVICE FIRMWARE © 2125 NANOSOFT
blade-server-rack-slot-status = Slot {$index}: {$content}
blade-server-rack-slot-entity-unknown = unknown
blade-server-rack-slot-empty = vacant
blade-server-rack-slot-eject = Eject
blade-server-rack-slot-insert = Insert
blade-server-rack-slot-power-toggle = Toggle Power
blade-server-rack-slot-locked-fail = It's locked!
blade-server-rack-slot-whitelist-fail = That doesn't fit!
blade-server-rack-examine-empty = It contains [color=#1f8ab2]no blades[/color].
blade-server-rack-examine-single = It contains only {$slot}.
blade-server-rack-examine-multiple-start = It contains
blade-server-rack-examine-multiple-slot-line = - {$slot}
blade-server-rack-examine-slot = { INDEFINITE($name) } [color=#1f8ab2]{ CAPITALIZE($name) }[/color] in slot {$index}
blade-server-rack-examine-distant =
    It contains [color=#1f8ab2]{$numBlades} { $numBlades ->
        [1] blade
        *[other] blades
    }[/color], but you can't tell what { $numBlades ->
        [1] it is
        *[other] they are
    } from this distance.
