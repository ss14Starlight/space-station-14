## Base actions

alerts-vampire-blood-name = Blood Drunk
alerts-vampire-blood-desc = Shows how much blood you've drunk. Extend your fangs and left-click a target to drink.

alerts-vampire-fed-name = Blood Fullness
alerts-vampire-fed-desc = Your current blood fullness. Drink blood to stay fed.

roles-antag-vamire-name = Vampire
roles-antag-vampire-description = Feed on the crew. Extend your fangs and drink their blood.

vampire-roundend-name = vampire

vampire-drink-start = You sink your fangs into {CAPITALIZE(THE($target))}.

vampire-not-enough-blood = Not enough blood.

vampire-mouth-covered = Your mouth is covered!
vampire-target-protected-by-faith = This person is protected by their faith!
vampire-drink-target-maxed = You have already drunk { $amount } units of blood from this target.
vampire-drink-target-hard-max = You have drunk the maximum amount of blood from this target ({ $amount } units).
vampire-full-power-achieved = Your vampiric essence surges full power achieved!

vampire-role-greeting = You are a vampire!
    Your blood thirst compels you to feed on crew members. Use your abilities to turn other crew.
    Your fangs allow you to suck blood from humans. Blood will regenerate health and give you new abilities.
    Find something to accomplish during this shift!

# Objectives
objective-issuer-vampire = [color=crimson]Vampire[/color]

objective-condition-drain-title = Drain {$count} units of blood
objective-condition-drain-description = Drink {$count} units of blood from crew members using your fangs.

objective-vampire-thrall-obey-master-title = Obey your master, {$targetName}.

# Class selection action
action-vampire-class-select = Select vampire class
action-vampire-class-select-desc = Choose your vampire subclass

# Round end statistics
roundend-prepend-vampire-drained-low = The vampires barely fed this shift, draining only {$blood} units of blood.
roundend-prepend-vampire-drained-medium = The vampires had a decent meal, draining {$blood} units of blood.
roundend-prepend-vampire-drained-high = The vampires had a blood feast, draining {$blood} units of blood!
roundend-prepend-vampire-drained-critical = The vampires went on a feeding frenzy, draining a staggering {$blood} units of blood!

roundend-prepend-vampire-drained = No vampires managed to drain any significant amount of blood this round.
roundend-prepend-vampire-drained-named = {$name} was the most bloodthirsty vampire, draining {$number} units of blood total.

# Vampire class selection tooltips
vampire-class-hemomancer-tooltip = Hemomancer
    Focuses on blood magic and the manipulation of blood around you
    
vampire-class-umbrae-tooltip = Umbrae
    Focuses on darkness, stealth ambushing and mobility

vampire-class-gargantua-tooltip = Gargantua
    Focuses on tenacity and melee damage

vampire-class-dantalion-tooltip = Dantalion
    Focuses on thralling and illusions

# Hemomancer abilities
action-vampire-hemomancer-tendrils-wrong-place = Cannot cast there.

action-vampire-blood-barrier-wrong-place = Cannot place barriers there.

action-vampire-sanguine-pool-already-in = You are already in sanguine pool form!
action-vampire-sanguine-pool-invalid-tile = You cannot become a blood pool here.
action-vampire-sanguine-pool-enter = You transform into a pool of blood!
action-vampire-sanguine-pool-exit = You reform from the blood pool!
vampire-space-burn-warning = The harsh void light scorches your undead flesh!

action-vampire-blood-eruption-activated = You cause blood to erupt in spikes around you!

action-vampire-blood-bringers-rite-not-enough-power = You lack full vampiric power (need above 1000 total blood & 8 unique victims)
action-vampire-blood-brighters-rite-not-enough-blood = Not enough blood to activate blood bringers rite
action-vampire-blood-bringers-rite-start = Blood Bringers Rite activated!
action-vampire-blood-bringers-rite-stop = Blood bringers rite deactivated
action-vampire-blood-bringers-rite-stop-blood = Blood Bringers Rite deactivated - not enough blood

vampire-locate-result = Your senses trace { $target } to { $location }.
vampire-locate-not-same-sector = vampire-locate-not-same-sector = That person is not on your sector.
vampire-locate-unknown = Unknown area
vampire-locate-no-targets = No prey can be sensed on this sector.

predator-sense-title = Predator Sense
vampire-locate-search-placeholder = Search...

vampiric-claws-remove-popup = You make claws disappear.

# Umbrae abilities
action-vampire-cloak-of-darkness-start = You blend into the shadows!
action-vampire-cloak-of-darkness-stop = You step out of the shadows.

action-vampire-shadow-snare-placed = You set a shadow snare trap.
action-vampire-shadow-snare-wrong-place = You can't place a trap here.
action-vampire-shadow-snare-scatter = You scattered the shadow trap.
vampire-shadow-snare-oldest-removed = Your old shadow snare dissipates.
ent-shadow-snare-ensnare = shadow snare

action-vampire-shadow-anchor-returned = You returned to the shadow anchor
action-vampire-shadow-anchor-installed = You've secured a spot in the shadows

action-vampire-shadow-boxing-start = You begin shadow boxing.
action-vampire-shadow-boxing-stop = Shadow boxing has been stoped.
action-vampire-shadow-boxing-ends = Shadow boxing ends.

action-vampire-dark-passage-wrong-place = The darkness here is impenetrable...
action-vampire-dark-passage-activated = You slipped through the darkness...

action-vampire-extinguish-activated = You absorbed the light around you...({$count})

action-vampire-eternal-darkness-not-enough-blood = You have run out of blood to sustain eternal darkness.
action-vampire-eternal-darkness-start = You conjured eternal darkness...
action-vampire-eternal-darkness-stop = The eternal darkness has dissipated...

#Dantalion
vampire-enthrall-start = You reach into {CAPITALIZE(THE($target))}'s mind...
vampire-enthrall-success = {CAPITALIZE(THE($target))} bends the knee and becomes your thrall.
vampire-enthrall-target = Your mind is overwhelmed by vampiric domination!
vampire-enthrall-limit = You cannot control any more thralls.
vampire-enthrall-invalid = That target cannot be enthralled.
vampire-thrall-released = The vampiric hold over you fades.

vampire-pacify-invalid = That target cannot be pacified.
vampire-pacify-success = {CAPITALIZE(THE($target))} succumbs to your overwhelming serenity.
vampire-pacify-target = A crushing calm drowns your will to fight!

vampire-subspace-swap-thrall = You cannot subspace swap with your thralls.
vampire-subspace-swap-dead = That mind is beyond your reach.
vampire-subspace-swap-failed = The subspace rift fizzles uselessly.
vampire-subspace-swap-success = Space twists as you trade places with {CAPITALIZE(THE($target))}!
vampire-subspace-swap-target = Reality warps and you are torn into a new position!

vampire-rally-thralls-success = {$count ->
    [one] Your call rallies a thrall back to your side!
    *[other] Your call rallies {$count} thralls back to your side!
}
vampire-rally-thralls-none = None of your thralls can answer the call.
vampire-thrall-holy-water-freed = The holy water purges the vampires hold on your mind!

vampire-blood-bond-start = Rivers of blood knit you to your thralls.
vampire-blood-bond-stop = You let the blood bond fall slack.
vampire-blood-bond-no-thralls = You have no enthralled servants to bond with.
vampire-blood-bond-stop-blood = The bond shreds itself; you lack the blood to sustain it.

action-vampire-not-enough-power = Your power is insufficient (need >1000 total blood & 8 unique victims).

# Gargantua 
vampire-blood-swell-start = Your muscles swell with unholy power
vampire-blood-swell-end = The blood rage subsides.

vampire-blood-rush-start = Blood surges through your limbs!
vampire-blood-rush-end = Your supernatural speed fades.

vampire-seismic-stomp-activate = The ground shudders beneath your fury!

vampire-overwhelming-force-start = Your presence becomes immovable.
vampire-overwhelming-force-stop = You relax your iron grip.
vampire-overwhelming-force-too-heavy = This object is far too heavy to move!
vampire-overwhelming-force-door-pried = You wrench the door open with brute strength.

vampire-demonic-grasp-hit = A demonic claw seizes you!
vampire-demonic-grasp-pull = The claw drags you toward the vampire!

vampire-charge-start = You barrel forward with unstoppable force!
vampire-charge-impact = You crash into {CAPITALIZE(THE($target))} with devastating force!


vampire-blood-swell-cancel-shoot = Your fingers don`t fit in the trigger guard!!

vampire-holy-place-burn = The sacred ground sears your unholy flesh!

alerts-vampire-blood-swell-name = Blood Swell
alerts-vampire-blood-swell-desc = Your muscles surge with unholy power.
alerts-vampire-blood-rush-name = Blood Rush
alerts-vampire-blood-rush-desc = Supernatural speed courses through your limbs.