transit-pod-arrived = The pod opens. Move to step out before it leaves again.
transit-pod-arrived-at = The pod opens at {$station}. Move to step out before it leaves again.
transit-pod-pushed = You shove the pod along the tube.
transit-pod-push-failed = The pod won't budge that way.
transit-pod-being-boarded = {CAPITALIZE(THE($user))} is putting you into a transit pod!
transit-pod-none = There is no pod here.
transit-pod-not-ready = The pod is not open yet.
transit-pod-full = The pod is full.
transit-pod-climbing-out = You start prying the pod open...
transit-pod-moving = You can't get out while the pod is moving.
transit-pod-crash = The pod slams into something!
transit-pod-stop-requested = You ask the pod to stop at the next station.
transit-pod-brake-pulling = You reach for the emergency brake...
transit-pod-braked = The emergency brake screeches and the pod grinds to a halt. Climb out while you can!
transit-station-stop-requested = You press the button. The next pod will stop here.
transit-pod-cargo-only = This pod only takes cargo.
transit-pod-passengers-only = This pod only takes passengers.
transit-pod-no-route = The pod finds no way to its destination and runs its usual route.
transit-pod-destination-set = The pod will go to {$destination}.
transit-pod-destination-any = The pod will go to the nearest free station.
transit-pod-destination-cleared = The pod will follow its usual route.
transit-station-access-denied = Access denied.
transit-station-depot = Pods are not boarded at a depot.
transit-station-pod-coming = A pod is already on its way.
transit-station-wrong-side = Board from the platform side.
transit-pod-turned-back = The way ahead is blocked. The pod turns back and stops at the next station.
transit-pod-trip-cancelled = The crash threw the pod off its route. It stops at the next station.
transit-station-pod-called = You call a pod. One is on its way.
transit-station-verb-destination = Choose destination

transit-station-ui-title = Destination
transit-station-ui-none = Usual route
transit-station-ui-any = Nearest free station
transit-station-ui-empty = No named stations can be reached from here.

transit-tube-direction-north = north
transit-tube-direction-south = south
transit-tube-direction-east = east
transit-tube-direction-west = west
transit-tube-direction-northeast = north-east
transit-tube-direction-northwest = north-west
transit-tube-direction-southeast = south-east
transit-tube-direction-southwest = south-west
transit-tube-side-left = left
transit-tube-side-right = right

transit-tube-examine-ramp = Takes pods between the floor and the tubes under or over it. Pods going too fast will not make the slope.
transit-tube-examine-support = It stands on a [color=yellow]support[/color] and holds up the tubes around it.
transit-tube-examine-over = It hangs over the floor, held by a support nearby.
transit-tube-examine-over-sagging = It is [color=red]sagging[/color]: nothing holds it up this far from a ramp or a support.
transit-tube-examine-under = It runs under the floor.
transit-tube-examine-under-unstable = It runs under the floor, too far from a ramp for the ground to hold: [color=red]no floor will stay[/color] over it.
transit-tube-sagging = The tube creaks and starts to sag!
transit-tube-collapsed = The tube comes crashing down!
transit-tube-floor-collapsed = The floor gives way over the tube!
transit-tube-examine-oneway = Lets pods through going [color=yellow]{$direction}[/color] only.
transit-tube-examine-oneway-off = The one-way gate is [color=gray]off[/color]: pods pass both ways.
transit-tube-examine-oneway-broken = The one-way gate is [color=red]fried[/color]: pods pass both ways.
transit-tube-examine-switch = Switched to the [color=yellow]{$side}[/color] branch ({$direction}).
transit-tube-examine-switch-broken = The switch is [color=red]sparking[/color] and flips at random.
transit-tube-examine-branch-on = The side branch is [color=green]on[/color]: pods go {$direction}.
transit-tube-examine-branch-off = The side branch is [color=gray]off[/color]: pods go straight on.
transit-tube-examine-branch-broken = The side switch is [color=red]sparking[/color] and flips at random.
transit-tube-examine-accelerator = Speeds pods going {$direction} up to [color=yellow]{$speed}[/color] tiles per second.
transit-tube-examine-accelerator-overloaded = The coils are [color=red]overloaded[/color].
transit-tube-examine-brake = Slows pods going {$direction} back to their normal speed.
transit-tube-examine-gate-closed = The gate is [color=red]closed[/color]: pods wait in front of it.
transit-tube-examine-gate-open = The gate is [color=green]open[/color].
transit-tube-examine-gate-broken = The gate is [color=red]jammed open[/color].
transit-tube-examine-unloader = Empties cargo pods to the [color=yellow]{$direction}[/color] as they pass.
transit-tube-examine-unloader-off = The unloader is [color=gray]off[/color].
transit-tube-examine-loader = Loads cargo pods with whatever lies to the [color=yellow]{$direction}[/color] as they pass.
transit-tube-examine-loader-off = The loader is [color=gray]off[/color].
transit-tube-examine-signal = Guards pods going [color=yellow]{$direction}[/color].
transit-tube-examine-turntable = Turns pods {$degrees} degrees{$clockwise ->
    [true] {" "}clockwise
   *[false] {""}
}.
transit-tube-examine-tagger = Destination tag: [color=yellow]{$tag}[/color].
transit-tube-examine-router = Routes tags [color=yellow]{$tags}[/color] to the {$direction} branch.

transit-station-examine-name = Station: [color=yellow]{$name}[/color].
transit-station-examine-request-stop = A request stop. Press the button to call the next pod in.
transit-station-examine-requested = A stop has been [color=yellow]requested[/color].
transit-station-examine-holding = Pods are being [color=yellow]held[/color] here.
transit-station-examine-empty = No pod is docked.
transit-station-examine-coming = A pod is [color=yellow]on its way[/color] here.
transit-pod-examine-stalled = It is [color=yellow]stuck[/color]. A good shove would get it going again.
transit-pod-examine-departs = Departs in [color=yellow]{$seconds}[/color] s.
transit-pod-examine-parked = Parked until called.
transit-pod-examine-target = Heading for [color=yellow]{$destination}[/color].
transit-pod-examine-any = Heading for the nearest free station.
transit-pod-examine-boosted = It is moving [color=red]dangerously fast[/color].

## Tube glass

transit-tube-material-reinforced = reinforced glass
transit-tube-material-plasma = reinforced plasma glass
transit-tube-material-uranium = reinforced uranium glass
transit-tube-glass-same = The tube is already made of this glass.
transit-tube-glass-not-enough = You need { $count } { $count ->
    [one] sheet
   *[other] sheets
} to reglaze the tube.
transit-tube-glass-reglazed = You reglaze the tube with { $material }.
transit-tube-glass-examine = It is made of [color=lightblue]{ $material }[/color].
