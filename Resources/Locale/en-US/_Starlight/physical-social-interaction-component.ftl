# Menu
social-interaction-component-verb = Social Interaction

# Wave
wave-verb = Wave
waving-success = You wave at { THE($target) }.
waving-success-others = { CAPITALIZE(THE($user)) } waves at {THE($target)}.
waving-emote = waves at {THE($target)}.
waving-emote-self = waves.

# Look
look-verb = Look
looking-success = You look at { THE($target) }.
looking-success-others = { CAPITALIZE(THE($user)) } looks at {THE($target)}.
looking-emote = looks at {THE($target)}.
looking-emote-self = looks at {REFLEXIVE($target)}.

# Sniff - for animals with noses
sniff-verb = Sniff
sniffing-success = You sniff around { THE($target) }.
sniffing-success-others = { CAPITALIZE(THE($user)) } sniffs around {THE($target)}.
sniffing-emote = sniffs around {THE($target)}.
sniffing-emote-self = sniffs {REFLEXIVE($target)}.

# Palpate - 'sniffing' but for creatures with antennae
palpate-antennae-verb = Palpate
palpate-antennae-success = You wave your antennae at { THE($target) }.
palpate-antennae-success-others = { CAPITALIZE(THE($user)) } waves {POSS-ADJ($user)} antennae at {THE($target)}.
palpate-antennae-emote = waves {POSS-ADJ($user)} antennae at {THE($target)}.
palpate-antennae-emote-self = waves {POSS-ADJ($user)} antennae at {REFLEXIVE($target)}.

# Pat
pat-verb = Pat
patting-success = You pat { THE($target) }.
patting-success-others = { CAPITALIZE(THE($user)) } pats {THE($target)}.
patting-emote = pats {THE($target)}.

# Pat alt. - you can pet dogs
petting-success = You pet { THE($target) } on {POSS-ADJ($target)} soft floofy head.
petting-success-others = { CAPITALIZE(THE($user)) } pets {THE($target)} on {POSS-ADJ($target)} soft floofy head.
petting-emote = pets {THE($target)} on {POSS-ADJ($target)} soft floofy head.

# Boop (nose)
boop-verb = Boop
booping-success = You boop { THE($target) } on {POSS-ADJ($target)} nose.
booping-success-others = { CAPITALIZE(THE($user)) } boops {THE($target)} on {POSS-ADJ($target)} nose.
booping-emote = boops {THE($target)} on {POSS-ADJ($target)} nose.

# Boop alt. - Resomi/Avali don't have noses
booping-snoot-success = You boop { THE($target) } on {POSS-ADJ($target)} snoot.
booping-snoot-success-others = { CAPITALIZE(THE($user)) } boops {THE($target)} on {POSS-ADJ($target)} snoot.
booping-snoot-emote = boops {THE($target)} on {POSS-ADJ($target)} snoot.

# Boop alt. - Cyclorites don't have noses
booping-rocky-success = You boop { THE($target) } on {POSS-ADJ($target)} rocky face.
booping-rocky-success-others = { CAPITALIZE(THE($user)) } boops {THE($target)} on {POSS-ADJ($target)} rocky face.
booping-rocky-emote = boops {THE($target)} on {POSS-ADJ($target)} rocky face.

# Boop alt. - Cyborgs and IPCs don't have noses
booping-borg-success = You boop { THE($target) } on {POSS-ADJ($target)} metal face.
booping-borg-success-others = { CAPITALIZE(THE($user)) } boops {THE($target)} on {POSS-ADJ($target)} metal face.
booping-borg-emote = boops {THE($target)} on {POSS-ADJ($target)} metal face.

# Boop alt. - Parrots and birds have beaks
booping-beak-success = You boop { THE($target) } on {POSS-ADJ($target)} beak.
booping-beak-success-others = { CAPITALIZE(THE($user)) } boops {THE($target)} on {POSS-ADJ($target)} beak.
booping-beak-emote = boops {THE($target)} on {POSS-ADJ($target)} beak.

# Boolt alt. - Generic fallback for creatures with no noses, like spiders
booping-generic-success = You boop { THE($target) } on {POSS-ADJ($target)} face.
booping-generic-success-others = { CAPITALIZE(THE($user)) } boops {THE($target)} on {POSS-ADJ($target)} face.
booping-generic-emote = boops {THE($target)} on {POSS-ADJ($target)} face.
