# Metrics and Trace Contract

This is what the VR layer needs from the broker. Send it to Anvay early. Every field here is something the system should be logging for its own observability anyway, so this is asking for very little that should not already exist.

Until the broker emits this, the experience reads `data/sample-trace.json`, which is hand written and follows the same schema.

---

## Trace file

A trace is one request, start to finish, as an ordered list of hops.

```json
{
  "trace_id": "string",
  "prompt_preview": "string, first ~40 chars, display only",
  "recorded_at": "ISO 8601 timestamp",
  "hops": [ Hop, ... ]
}
```

## Hop

| Field | Type | Required | Meaning |
|---|---|:---:|---|
| `hop` | string | yes | One of `client`, `broker`, `scheduler`, `mini`, `rig`, `model` |
| `node_id` | string | yes | Which Mini or which rig, e.g. `rig-2`, `mini-07` |
| `t_start_ms` | number | yes | Milliseconds from trace start |
| `t_end_ms` | number | yes | Milliseconds from trace start |
| `status` | string | yes | `ok`, `queued`, `failed` |
| `label` | string | no | Short display string for the world space UI |

## Rig hops only

| Field | Type | Meaning |
|---|---|---|
| `gpu_index` | int | Which of the 8 cards, 0 indexed |
| `gpu_util` | number | Percent, 0 to 100 |
| `vram_used_mb` | number | Megabytes |
| `temp_c` | number | Celsius |
| `model` | string | Model name |
| `quantization` | string | e.g. `Q4_K_M` |

## Model hops only

| Field | Type | Meaning |
|---|---|---|
| `tokens_out` | int | Output token count |
| `tokens_per_sec` | number | Throughput |

---

## Notes for Anvay

- **Ordering by timestamp, not array position.** The VR layer sorts by `t_start_ms` so out of order writes are fine.
- **Relative times.** Everything is milliseconds from trace start rather than wall clock, so a trace replays identically regardless of when it was recorded.
- **Unknown hop types are skipped, not errors.** Adding a new hop type will not break the headset build. It just will not be drawn until I add a visual for it.
- **A failed hop is worth recording.** A trace where a card drops mid inference would be a genuinely good thing to show people, and it is the kind of failure my rig work is supposed to surface anyway.
- **Read only.** The endpoint the headset hits should expose traces and nothing else. No commands, no auth, no write path. The headset gets passed around a classroom.

## Endpoint, for live mode later

```
GET /traces/latest    -> one Trace object
GET /traces/{id}      -> one Trace object
```

Live mode is V8 and optional. The contract matters now because logging these fields from the start costs nothing and adding them later is painful.
