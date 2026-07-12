### Popups
reactor-smoke-start = {CAPITALIZE(THE($owner))} begins to smoke!
reactor-smoke-stop = {CAPITALIZE(THE($owner))} stops smoking.
reactor-fire-start = {CAPITALIZE(THE($owner))} begins to burn!
reactor-fire-stop = {CAPITALIZE(THE($owner))} stops burning.

reactor-unanchor-melted = You cannot unanchor {THE($owner)}, it's melted into the hull!
reactor-unanchor-warning = You cannot unanchor {THE($owner)} while it's not empty or hotter than 80C!
reactor-anchor-warning = Invalid anchor position.

### Messages
reactor-smoke-start-message = ALERT: {CAPITALIZE(THE($owner))} has reached a dangerous temperature: {$temperature}K. Intervene immediately to prevent meltdown.
reactor-smoke-stop-message = {CAPITALIZE(THE($owner))} has cooled below dangerous temperature. Have a nice day.
reactor-fire-start-message = ALERT: {CAPITALIZE(THE($owner))} has reached CRITICAL temperature: {$temperature}K. MELTDOWN IMMINENT.
reactor-fire-stop-message = {CAPITALIZE(THE($owner))} has cooled below critical temperature. Meltdown averted.

reactor-temperature-dangerous-message = {CAPITALIZE(THE($owner))} is at dangerous temperature: {$temperature}K.
reactor-temperature-critical-message = {CAPITALIZE(THE($owner))} is at critical temperature: {$temperature}K.
reactor-temperature-cooling-message = {CAPITALIZE(THE($owner))} is cooling: {$temperature}K.

reactor-melting-announcement = A nuclear reactor aboard the station is beginning to meltdown. Evacuation of the surrounding area is advised.
reactor-melting-announcement-sender = Nuclear Emergency

reactor-meltdown-announcement = A nuclear reactor aboard the station has catastrophically overloaded. Radioactive debris, nuclear fallout, and coolant fires are likely. Immediate evacuation of the surrounding area is strongly advised.
reactor-meltdown-announcement-sender = Nuclear Meltdown

### UI
comp-nuclear-reactor-ui-locked = Locked
comp-nuclear-reactor-ui-insert-button = Insert
comp-nuclear-reactor-ui-remove-button = Remove
comp-nuclear-reactor-ui-eject-button = Eject

comp-nuclear-reactor-ui-view-change = Change View
comp-nuclear-reactor-ui-view-temp = Temperature View
comp-nuclear-reactor-ui-view-neutron = Neutron View
comp-nuclear-reactor-ui-view-fuel = Fuel View

comp-nuclear-reactor-ui-status-panel = Reactor Status
comp-nuclear-reactor-ui-reactor-temp = Temperature
comp-nuclear-reactor-ui-reactor-rads = Radiation
comp-nuclear-reactor-ui-reactor-therm = Thermal Power
comp-nuclear-reactor-ui-reactor-control = Control Rods
comp-nuclear-reactor-ui-therm-format = { POWERWATTS($power) }t

comp-nuclear-reactor-ui-footer-left = Danger: high radiation.
comp-nuclear-reactor-ui-footer-right = 1.0 REV 1
