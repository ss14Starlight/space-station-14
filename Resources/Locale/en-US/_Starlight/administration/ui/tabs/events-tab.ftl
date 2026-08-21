administration-ui-events-tab-loading = Loading station events...
administration-ui-events-tab-search-label = Search
administration-ui-events-tab-filter-placeholder = Filter events
administration-ui-events-tab-sort-label = Sort
administration-ui-events-tab-sort-name = Name
administration-ui-events-tab-sort-state = State
administration-ui-events-tab-sort-availability = Availability
administration-ui-events-tab-sort-duration = Duration
administration-ui-events-tab-sort-weight = Weight
administration-ui-events-tab-sort-min-players = Min players
administration-ui-events-tab-sort-start = Earliest start
administration-ui-events-tab-sort-cooldown = Cooldown
administration-ui-events-tab-sort-ascending = Ascending
administration-ui-events-tab-sort-descending = Descending
administration-ui-events-tab-available-only = Available only
administration-ui-events-tab-refresh = Refresh
administration-ui-events-tab-force = Force
administration-ui-events-tab-force-disabled = You do not have permission to run gamerules.
administration-ui-events-tab-queue-title = Upcoming events
administration-ui-events-tab-queue-no-scheduler = No compatible event scheduler is active this round.
administration-ui-events-tab-queue-empty = The event queue is empty.
administration-ui-events-tab-queue-count = { $count ->
    [one] { $count } event queued.
   *[other] { $count } events queued.
}
administration-ui-events-tab-queue-automatic = AUTO: {$event} ({$id})
administration-ui-events-tab-queue-manual = SCHEDULED: {$event} ({$id})
administration-ui-events-tab-queue-starts-in = Starts in {$time}
administration-ui-events-tab-queue-minus-5 = -5m
administration-ui-events-tab-queue-minus-1 = -1m
administration-ui-events-tab-queue-plus-1 = +1m
administration-ui-events-tab-queue-plus-5 = +5m
administration-ui-events-tab-queue-now = Now
administration-ui-events-tab-queue-cancel = Cancel
administration-ui-events-tab-active-title = Active events
administration-ui-events-tab-active-empty = No station events are currently active.
administration-ui-events-tab-active-count = { $count ->
    [one] { $count } station event active.
   *[other] { $count } station events active.
}
administration-ui-events-tab-active-remaining = {$remaining} remaining of {$duration}
administration-ui-events-tab-active-open = Variable duration | elapsed {$elapsed}
administration-ui-events-tab-active-end = End
administration-ui-events-tab-catalog-title = Event catalog
administration-ui-events-tab-schedule = Schedule
administration-ui-events-tab-schedule-minutes-placeholder = min
administration-ui-events-tab-schedule-no-scheduler = No compatible scheduler is active.
administration-ui-events-tab-enabled = enabled
administration-ui-events-tab-disabled = disabled
administration-ui-events-tab-summary = Events: {$count} | Active: {$active} | Pending: {$pending} | Players: {$players} | Round: {$minutes} min | Scheduler: {$enabled}
administration-ui-events-tab-status-available = Auto: available
administration-ui-events-tab-status-unavailable = Auto: unavailable
administration-ui-events-tab-meta = Min players {$players} | Start {$start}m | Cooldown {$cooldown}m | Weight {$weight}
administration-ui-events-tab-runtime-idle = State: idle
administration-ui-events-tab-runtime-pending = State: pending x{$count}
administration-ui-events-tab-runtime-active = State: active x{$count}
administration-ui-events-tab-runtime-next-start = Starts in {$time}
administration-ui-events-tab-runtime-remaining = Remaining {$time}
administration-ui-events-tab-runtime-remaining-range = Remaining {$min}-{$max}
administration-ui-events-tab-runtime-duration = Duration {$duration}
administration-ui-events-tab-duration-open = variable
administration-ui-events-tab-duration-range = {$min}-{$max}
administration-ui-events-tab-queue-scheduler = Scheduler: { $scheduler }
administration-ui-events-tab-queue-incomplete = { $count ->
    [one] Incomplete: { $count } active scheduler does not expose a queue, so some events are not listed here.
   *[other] Incomplete: { $count } active schedulers do not expose a queue, so some events are not listed here.
}
administration-ui-events-tab-schedule-minutes-tooltip = Delay in minutes before the event fires
administration-ui-events-tab-schedule-minus-tooltip = One minute sooner
administration-ui-events-tab-schedule-plus-tooltip = One minute later
administration-ui-events-tab-collapse-tooltip = Collapse or expand this section
administration-ui-events-tab-meta-occurrences = { $count ->
    [one] Ran { $count } time this round
   *[other] Ran { $count } times this round
}
