## Examine

combustion-turbine-examine-part = It has a [color=lightblue]tier { $tier }[/color] part installed.
combustion-turbine-examine-no-part = [color=yellow]Its turbine part is missing.[/color]
combustion-turbine-examine-unassembled = [color=yellow]It is not assembled with a core rotor.[/color]
combustion-turbine-examine-rpm = The rotor is spinning at [color=orange]{ $rpm }[/color] RPM.
combustion-turbine-examine-integrity = Rotor integrity: [color=orange]{ $integrity }%[/color].
combustion-turbine-examine-power = It is generating [color=yellow]{ POWERWATTS($power) }[/color].

## Assembly

combustion-turbine-assembly-ready = The turbine is fully assembled.
combustion-turbine-assembly-missing-compressor = [color=yellow]It needs an inlet compressor behind the core rotor, facing the same way.[/color]
combustion-turbine-assembly-missing-outlet = [color=yellow]It needs a turbine outlet in front of the core rotor, facing the same way.[/color]
combustion-turbine-assembly-panel-open = [color=yellow]A maintenance panel on the turbine is open.[/color]
combustion-turbine-assembly-missing-part = [color=yellow]A section of the turbine is missing its part.[/color]

## Parts

combustion-turbine-part-wrong-stage = This part does not fit in this section.
combustion-turbine-part-panel-closed = Open the maintenance panel first.
combustion-turbine-part-running = Stop the turbine first.
combustion-turbine-part-occupied = Pry the installed part out first.
combustion-turbine-part-max-tier = This part is already at its highest tier.
combustion-turbine-part-wrong-material = That is not the right material to upgrade this part.
combustion-turbine-part-not-enough = You need { $amount } { $material } to upgrade this part.
combustion-turbine-part-upgraded = The part is now tier { $tier }.
combustion-turbine-part-examine-tier = A [color=lightblue]tier { $tier }[/color] part, rated for [color=orange]{ $rpm }[/color] RPM and [color=orange]{ $temperature } K[/color].
combustion-turbine-part-examine-compressor = Compressor intake: [color=orange]{ $value }%[/color].
combustion-turbine-part-examine-rotor = Rotor friction: [color=orange]{ $value }%[/color].
combustion-turbine-part-examine-stator = Stator efficiency: [color=orange]{ $value }%[/color].
combustion-turbine-part-examine-upgrade = It can be upgraded with [color=yellow]{ $amount } { $material }[/color].
combustion-turbine-part-examine-max-tier = It is at its highest tier.

## Radio

combustion-turbine-alert-damage = Warning: combustion turbine near { $location } is taking damage! Rotor integrity at { $integrity }%.
combustion-turbine-alert-failure = Combustion turbine near { $location } has suffered a catastrophic rotor failure!

## Console

combustion-turbine-console-title = Combustion Turbine Control
combustion-turbine-console-status-connected = Turbine connected
combustion-turbine-console-status-unlinked = No turbine linked. Link the core rotor with a multitool.
combustion-turbine-console-status-no-compressor = The core rotor has no inlet compressor behind it.
combustion-turbine-console-status-no-outlet = The core rotor has no turbine outlet in front of it.
combustion-turbine-console-status-panel-open = A maintenance panel on the turbine is open.
combustion-turbine-console-status-missing-part = A turbine section is missing its part.
combustion-turbine-console-power = Turbine power
combustion-turbine-console-on = On
combustion-turbine-console-off = Off
combustion-turbine-console-starter = Starter motor engaged...
combustion-turbine-console-governor = Speed governor holding the rotor: intake at { $value }%
combustion-turbine-console-overheat = OVERHEATING: intake gas is hotter than the parts are rated for!
combustion-turbine-console-rpm = Rotor speed
combustion-turbine-console-rpm-value = { $rpm } / { $max } RPM
combustion-turbine-console-integrity = Rotor integrity
combustion-turbine-console-output = Power output
combustion-turbine-console-power-value = { POWERWATTS($power) }
combustion-turbine-console-inlet-temperature = Intake temperature
combustion-turbine-console-temperature-value = { $temperature } K (parts rated { $max } K)
combustion-turbine-console-exhaust-temperature = Exhaust temperature
combustion-turbine-console-kelvin-value = { $temperature } K
combustion-turbine-console-inlet-pressure = Intake pressure
combustion-turbine-console-outlet-pressure = Exhaust pressure
combustion-turbine-console-pressure-value = { $pressure } kPa
combustion-turbine-console-pressure-ratio = Expansion ratio
combustion-turbine-console-pressure-ratio-value = { $ratio } : 1
combustion-turbine-console-pressure-ratio-vacuum = venting to vacuum
combustion-turbine-console-pressure-ratio-stalled = stalled, exhaust pressure too high
combustion-turbine-console-flow = Gas flow
combustion-turbine-console-flow-value = { $flow } mol/s
combustion-turbine-console-intake = Intake regulator
combustion-turbine-console-percent = { $value }%
combustion-turbine-console-intake-value = { $value }% ({ $volume } L/s)
combustion-turbine-console-parts = Part tiers
combustion-turbine-console-parts-value = compressor { $compressor }, rotor { $rotor }, stator { $stator }

combustion-turbine-slot-part = Turbine part
