health-change-display =
    { $deltasign ->
        [-1] [color=green]{NATURALFIXED($amount, 2)}[/color] {$kind}
        *[1] [color=red]{NATURALFIXED($amount, 2)}[/color] {$kind}
    }

health-change-mixmax-display = [color=green]{NATURALFIXED($amount, 2)}[/color] across {$targets}, prioritizing the most damaged
