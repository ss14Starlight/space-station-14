## Console

station-goal-console-title = Station Goals
station-goal-console-active-header = Active goals
station-goal-console-active-count = {$count} of {$max} slots taken
station-goal-console-free-slot = Free goal slot
station-goal-console-no-station = This console is not connected to a station.
station-goal-console-offers = Offered by Central Command
station-goal-console-no-offers = Central Command has no goals to offer right now.
station-goal-console-refresh = New cards in {$time}
station-goal-console-refresh-paused = Cards are locked while voting
station-goal-console-vote-hint = Command can start a vote to pick one of the offered goals.
station-goal-console-vote-running = Voting: {$time} left, {$cast} of {$voters} command members voted
station-goal-console-start-vote = Start command vote
station-goal-console-print = Print goals
station-goal-console-vote = Vote ({$count})
station-goal-console-objectives = Objectives
station-goal-console-reward-department = Each department account
station-goal-console-reward-personal = Each crew member
station-goal-console-credits = {$amount} cr
station-goal-console-time-limit = Time limit
station-goal-console-time-left = Time left
station-goal-console-minutes = {$minutes} min
station-goal-console-unavailable = Overlaps with an active goal, or no goal slot is free.
station-goal-console-vote-unavailable = There is nothing to vote on right now.
station-goal-console-access-denied = Access denied.

## Announcements and rewards

station-goal-announcement-sender = Central Command
station-goal-announcement-activated = The station has accepted a new goal: {$name}. Departments involved: {$departments}. Command can print the full requirements at the station goals console.
station-goal-announcement-completed = Station goal completed: {$name}. {$departments} receive {$departmentReward} credits on their department accounts, and each crew member of these departments receives {$personalReward} credits.
station-goal-announcement-failed = The station goal "{$name}" has run out of time and was withdrawn.
station-goal-reward-message = You received {$amount} credits for completing the station goal "{$goal}".

## Printed sheet

station-goal-paper-name = station goals
station-goal-paper-header = [head=2]Station goals of {$station}[/head]
station-goal-paper-none = The station has no active goals.
station-goal-paper-goal = [head=3]{$name}[/head]
station-goal-paper-departments = [bold]Departments:[/bold] {$departments}
station-goal-paper-objectives = [bold]Objectives:[/bold]
station-goal-paper-objective = - {$text} ({$current}/{$target})
station-goal-paper-deadline = [bold]Time left:[/bold] {$minutes} min
station-goal-paper-rewards = [bold]Reward:[/bold] {$departmentReward} cr per department, {$personalReward} cr per crew member

## Goals

station-goal-food-supply-name = Rations for the NT division
station-goal-food-supply-description = A NanoTrasen division is passing through the sector and needs proper food. Cook a varied menu and ship it through cargo.
station-goal-food-supply-objective = Ship {$kinds} different whole dishes, {$amount} of each. Slices do not count

station-goal-exotic-harvest-name = Exotic harvest
station-goal-exotic-harvest-description = Central Command is cataloging plant species. Grow and mutate as many different plants as you can and ship them through cargo.
station-goal-exotic-harvest-objective = Ship {$kinds} different kinds of produce, {$amount} of each

station-goal-artifact-shipment-name = Artifact survey
station-goal-artifact-shipment-description = The research division wants alien artifacts. Find artifacts, research them and sell.
station-goal-artifact-shipment-objective = Ship {$amount} alien artifacts

station-goal-bsh-name = Bluespace harvest
station-goal-bsh-description = Central Command wants a large bluespace yield. Push the harvester to dangerous levels and hold the line against whatever comes through the portals.
station-goal-bsh-objective-danger = Keep the bluespace harvester at level 7 or higher for {$target} seconds in total
station-goal-bsh-objective-points = Harvest {$target} bluespace points

station-goal-bsa-name = Bluespace artillery
station-goal-bsa-description = Build bluespace artillery and prove it works on the NanoTrasen test range.
station-goal-bsa-instructions = Research Bluespace Artillery, then print generator, fusor, bore and control console boards. Build three machines in one row with the generator at the back, fusor in the middle and bore at the front, inside a powered room at least 11 by 3 tiles. Point bore at space: the shot wrecks everything in front of it. Run a high voltage cable under the fusor. Link fusor to the generator, bore and control console with a multitool, then press Assemble on console. Pick test range as target, let cannon charge and fire.
station-goal-bsa-objective = Destroy the Nanotrasen artillery test range

station-goal-meteor-shield-name = Meteor shield
station-goal-meteor-shield-description = Central Command is tired of patching holes. Set up a ring of defence satellites that shoots down meteors before they reach the hull.
station-goal-meteor-shield-instructions = Cargo can now order meteor shield satellites and control console board. Build console, drag satellites out into open space around the station and switch them on, by hand or from the console. Each satellite covers 14 tiles and needs a clear line of sight, so spread them out until the console shows enough hull coverage.
station-goal-meteor-shield-objective = Reach {$target}% meteor shield coverage of the station hull

## Command

cmd-stationgoal-desc = Manages station goals.
cmd-stationgoal-help = Usage: stationgoal list | refresh | vote | activate <goal> | progress <key> [value] | complete <goal> | cancel <goal>
