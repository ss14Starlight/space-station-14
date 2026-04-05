### Examine

gas-turbine-examine-stator-null = It seems to be missing a stator.
gas-turbine-examine-stator = It has a stator.

gas-turbine-examine-blade-null = It seems to be missing a turbine blade.
gas-turbine-examine-blade = It has a turbine blade.

gas-turbine-spinning-0 = The blades are not spinning.
gas-turbine-spinning-1 = The blades are turning slowly.
gas-turbine-spinning-2 = The blades are spinning.
gas-turbine-spinning-3 = The blades are spinning quickly.
gas-turbine-spinning-4 = [color=red]The blades are spinning out of control![/color]

gas-turbine-damaged-0 = It appears to be in good condition.[/color]
gas-turbine-damaged-1 = The turbine looks a bit scuffed.[/color]
gas-turbine-damaged-2 = [color=yellow]The turbine looks badly damaged.[/color]
gas-turbine-damaged-3 = [color=orange]It's critically damaged![/color]

gas-turbine-ruined = [color=red]It's completely broken![/color]

### Popups

# Shown when an event occurs
gas-turbine-overheat = {$owner} triggers the emergency overheat dump valve!
gas-turbine-explode = {CAPITALIZE(THE($owner))} tears itself apart!

# Shown when damage occurs
gas-turbine-spark = {CAPITALIZE(THE($owner))} starts sparking!
gas-turbine-spark-stop = {CAPITALIZE(THE($owner))} stops sparking.
gas-turbine-smoke = {CAPITALIZE(THE($owner))} begins to smoke!
gas-turbine-smoke-stop = {CAPITALIZE(THE($owner))} stops smoking.

# Shown during repairs
gas-turbine-repair-fail-blade = You need to replace the turbine blade before this can be repaired.
gas-turbine-repair-fail-stator = You need to replace the stator before this can be repaired.
gas-turbine-repair-ruined = You repair {THE($target)}'s casing with {THE($tool)}.
gas-turbine-repair-partial = You repair some of the damage to {THE($target)} using {THE($tool)}.
gas-turbine-repair-complete = You finish repairing {THE($target)} with {THE($tool)}.
gas-turbine-repair-no-damage = There is no damage to repair on {THE($target)} using {THE($tool)}.

# Anchoring warnings
gas-turbine-unanchor-warning = You cannot unanchor {THE($owner)} while the turbine is spinning!
gas-turbine-anchor-warning = Invalid anchor position.

gas-turbine-eject-fail-speed = You cannot remove turbine parts while the turbine is spinning!
gas-turbine-insert-fail-speed = You cannot insert turbine parts while the turbine is spinning!

### UI

# Shown when using the UI
gas-turbine-ui-tab-main = Controls
gas-turbine-ui-tab-parts = Parts

gas-turbine-ui-rpm = RPM

gas-turbine-ui-overspeed = OVERSPEED
gas-turbine-ui-overtemp = OVERTEMP
gas-turbine-ui-stalling = STALLING
gas-turbine-ui-undertemp = UNDERTEMP

gas-turbine-ui-flow-rate = Flow Rate
gas-turbine-ui-stator-load = Stator Load

gas-turbine-ui-blade = Turbine Blade
gas-turbine-ui-blade-integrity = Integrity
gas-turbine-ui-blade-stress = Stress

gas-turbine-ui-stator = Turbine Stator
gas-turbine-ui-stator-potential = Potential
gas-turbine-ui-stator-supply = Supply

gas-turbine-ui-power = { POWERWATTS($power) }

gas-turbine-ui-locked-message = Controls locked.
gas-turbine-ui-footer-left = Danger: fast-moving machinery.
gas-turbine-ui-footer-right = 2.1 REV 1
