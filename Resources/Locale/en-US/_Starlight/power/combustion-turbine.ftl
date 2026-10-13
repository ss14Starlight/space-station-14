## Examine

combustion-turbine-examine-connected = It is connected to its [color=lightblue]turbine[/color].
combustion-turbine-examine-no-generator = [color=yellow]It needs a turbine anchored behind it, facing the other way.[/color]
combustion-turbine-examine-no-compressor = [color=yellow]It needs a compressor anchored behind it, facing the other way.[/color]
combustion-turbine-examine-rpm = The rotor is spinning at [color=orange]{ $rpm }[/color] RPM.
combustion-turbine-examine-integrity = Rotor integrity: [color=orange]{ $integrity }%[/color].
combustion-turbine-examine-power = It is generating [color=yellow]{ POWERWATTS($power) }[/color].

## Radio

combustion-turbine-alert-damage = Warning: combustion turbine near { $location } is taking damage! Rotor integrity at { $integrity }%.
combustion-turbine-alert-failure = Combustion turbine near { $location } has suffered a catastrophic rotor failure!

## Console

combustion-turbine-console-title = Combustion Turbine Control
combustion-turbine-console-status-connected = Turbine connected
combustion-turbine-console-status-unlinked = No turbine linked. Link one with a multitool.
combustion-turbine-console-status-no-generator = The compressor has no turbine behind it.
combustion-turbine-console-power = Turbine power
combustion-turbine-console-on = On
combustion-turbine-console-off = Off
combustion-turbine-console-starter = Starter motor engaged...
combustion-turbine-console-governor = Speed governor holding the rotor: intake at { $value }%
combustion-turbine-console-overheat = OVERHEATING: intake gas is hotter than the blades are rated for!
combustion-turbine-console-rpm = Rotor speed
combustion-turbine-console-rpm-value = { $rpm } / { $max } RPM
combustion-turbine-console-integrity = Rotor integrity
combustion-turbine-console-output = Power output
combustion-turbine-console-power-value = { POWERWATTS($power) }
combustion-turbine-console-inlet-temperature = Intake temperature
combustion-turbine-console-temperature-value = { $temperature } K (blades rated { $max } K)
combustion-turbine-console-inlet-pressure = Intake pressure
combustion-turbine-console-chamber-pressure = Chamber pressure
combustion-turbine-console-outlet-pressure = Exhaust pressure
combustion-turbine-console-pressure-value = { $pressure } kPa
combustion-turbine-console-flow = Gas flow
combustion-turbine-console-flow-value = { $flow } mol/s
combustion-turbine-console-intake = Intake regulator
combustion-turbine-console-percent = { $value }%
combustion-turbine-console-intake-value = { $value }% ({ $volume } L/s)
