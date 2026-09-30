```powershell
dotnet run --project Content.Server -- --cvar metrics.enabled=true --cvar loki.enabled=true --cvar loki.name=local --cvar loki.address=http://127.0.0.1:3100
```

Both dashboards are backed by `sl_round_stat` structured Loki records.

## Record schema

Every record starts with `sl_round_stat schema=1` and carries `fork`, `round_id` and `kind`. `fork` is the server's `build.fork_id` CVar, or `unknown` when it is unset. Any value containing whitespace, `"` or `=` is quoted.

| `kind` | One record per | Fields |
| --- | --- | --- |
| `round_summary` | round | `preset_id`, `resolved_preset_id`, `map_id`, `duration_seconds`, `players_roundstart`, `players_latejoin`, `players_peak`, `players_end`, `players_connected`, `antags`, `observers` |
| `population` | round | `alive`, `critical`, `dead`, `bodyless`, `deaths` |
| `job_preference` | job and priority | `job_id`, `priority`, `players` |
| `job_selection` | job | `job_id`, `candidates`, `available_slots`, `slots_recorded`, `slots_unlimited`, `round_start_assignments`, `late_join_assignments` |
| `job_spawn` | species, job and phase | `species_id`, `job_id`, `spawn_phase`, `count` |
| `antag_spawn` | antagonist type | `type_id`, `count` |
| `antag_selection` | rule and type | `rule_id`, `type_id`, `expected`, `eligible`, `preselected`, `assigned`, `ghost_roles`, `unassigned`, `uncovered`, `forced_assignments`, `ghost_roles_created`, `latejoin_assignments` |
| `antag_outcome` | rule result | `type_id`, `result`, `count`, plus per-antagonist fields |
| `antag_outcome_stat` | measurement | `type_id`, `stat`, `value` |
| `antag_choice` | choice | `type_id`, `choice_type`, `choice_id`, `count` |
| `objective` | rule and objective | `rule_id`, `objective_id`, `assigned`, `completed`, `progress_sum` |
| `dynamic_rule` | rule started by Dynamic | `rule_id`, `roundstart`, `priced`, `count`, `cost_total` |
| `dynamic_summary` | round | `rules_run`, `cost_total`, `budget_remaining`, `budget_peak` |
| `store_purchase` | listing | `store_id`, `item_id`, `discounted`, `count` |
| `store_currency` | store and currency | `store_id`, `currency_id`, `spent`, `remaining`, `stores` |
| `vote_result` | vote option | `vote_type`, `option_id`, `votes`, `winner`, `for_next_round` |
| `secure_terminal` | secure command terminal request | `request_id`, `action_type`, `proposed`, `reasons`, `signatures`, `admin_approvals`, `authorized`, `executed`, `denied`, `expired`, `recalled`, `pending_seconds_total`, `fee_held`, `fee_refunded`, `salary_penalty_total` |
| `secure_terminal_summary` | round | `requests`, `proposed`, `authorized`, `executed`, `denied`, `expired`, `recalled`, `signatures`, `fee_held`, `fee_refunded`, `salary_penalty` |
| `ghost_role` | ghost role | `role_id`, `job_id`, `offered`, `slots`, `taken` |
| `ghost_role_summary` | round | `roles`, `offered`, `slots`, `taken` |

Both summary records are emitted even when nothing happened.

```logql
#totas antagonist assignments over a selected time range
sum by (type_id) (
  max_over_time(
    {App="Robust.Server", Server=~`${Server:raw}`}
      |= "sl_round_stat"
      | logfmt
      | kind="antag_spawn"
      | round_id=~`${Round:raw}`
      | unwrap count
    [$__range]
  )
)
```

```logql
# how many of the slots of ERT with were taken
sum by (job_id) (
  max_over_time(
    {App="Robust.Server", Server=~`${Server:raw}`}
      |= "sl_round_stat"
      | logfmt
      | kind="ghost_role"
      | job_id=~`ERT.*|CBURN|DeathSquad`
      | round_id=~`${Round:raw}`
      | unwrap taken
    [$__range]
  )
)
```
