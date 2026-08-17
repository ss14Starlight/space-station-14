cmd-grantrole-desc = Grant a job or antag role to a player, bypassing playtime requirements.
cmd-grantrole-help = Usage: grantrole <player> <role prototype id>
    Example: grantrole Urist Captain
    Uses the job-whitelist database. Role bans still apply.

cmd-grantrole-already-granted = {$player} already has a grant for {$roleName} ({$roleId}).
cmd-grantrole-granted = Granted {$roleName} ({$roleId}) to {$player}. Playtime requirements are bypassed for that role.

cmd-revokerole-desc = Revoke a previously granted role; playtime requirements will apply again.
cmd-revokerole-help = Usage: revokerole <player> <role prototype id>
    Example: revokerole Urist Captain

cmd-revokerole-not-granted = {$player} has no grant for {$roleName} ({$roleId}).
cmd-revokerole-revoked = Revoked {$roleName} ({$roleId}) from {$player}. Playtime requirements will apply again.
