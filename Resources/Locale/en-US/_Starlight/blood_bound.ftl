admin-verb-make-blood-bound = Make the target into a Blood Bound.

admin-verb-text-make-blood-bound = Make Blood Bound

blood-bound-round-end-agent-name = blood bound

objective-issuer-blood-bound = [color=mediumvioletred]The Blood Oath[/color]

blood-bound-round-end-no-mind = [color=white]{CAPITALIZE($name)}[/color] ([color=gray]{$username}[/color]) was oath-bound with [color=white]{$partnerName}[/color].
blood-bound-round-end = [color=white]{CAPITALIZE($name)}[/color] ([color=gray]{$username}[/color]) was oath-bound with [color=white]{$partnerName}[/color] ([color=gray]{$partnerUsername}[/color]).

blood-bound-recruiter-greeting =
    You are a Blood Bound.
    You have been given the ability to take a blood oath with one member of the crew.
    Use this ability wisely and finish your objectives given to you by the Children of Nar'Sie.

blood-bound-role-greeting =
    You have been sworn into a blood oath.
    Listen to your Blood Bound and help them accomplish their objectives

blood-bound-briefing = Collaborate with your Blood Bound to accomplish all your objectives.

# Conversion

blood-bound-convert-failed-no-mind = {CAPITALIZE(THE($converted))} does not have a mind
blood-bound-convert-failed-already-bound = {CAPITALIZE(THE($converted))} is already a Blood Bound
blood-bound-convert-failed-target = {CAPITALIZE(THE($converted))} is your target
blood-bound-convert-failed-zombie = {CAPITALIZE(THE($converted))} is a zombie
blood-bound-convert-failed-shielded = {CAPITALIZE(THE($converted))}'s mind is too protected
blood-bound-convert-failed-dead = {CAPITALIZE(THE($converted))} is dead
blood-bound-convert-failed-preference = {CAPITALIZE(THE($converted))}'s mind is unaffected by the conversion

blood-bound-convert-convertible = {CAPITALIZE(THE($converted))} is convertible
blood-bound-conversion-popup = {CAPITALIZE(THE($converter))} grabs {THE($converted)} firmly and digs deep symbols into their arms.

guide-entry-blood-bound = Blood Bound

role-subtype-blood-bound = Blood Bound

roles-antag-blood-bound-name = Blood Bound
roles-antag-blood-bound-objective = On behalf of Nar'Sie, collaborate with your blood bound to accomplish all your objectives.

blood-bound-convert-failed-bound = You have already taken your blood oath.

objective-condition-blood-bound-progress-title = Help {$targetName} accomplish their objectives.

objective-condition-blood-bound-steal-description = We need to get the {$itemName}.
objective-condition-blood-bound-steal-multiply-description = We need to get {$count} {MAKEPLURAL($itemName)}.

objective-condition-blood-bound-steal-heart = Steal the heart of {$targetName}, {CAPITALIZE($job)}.
objective-condition-blood-bound-firestake = Firestake {$count} { $count ->
    [one] person
    *[other] people
    } in the {$zone} location.
objective-condition-blood-bound-firestake-description = We need to burn {$count} { $count ->
    [one] person
    *[other] people
    } on bonfire stakes in {$zone} while they are still alive.
