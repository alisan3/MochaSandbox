# Mocha Saga Concurrency Bug — Handover Report

**Date:** 2026-07-09
**Repo:** `alisan3/MochaSandbox` (branch `main`)

## Environment

- **Mocha** 16.2.3
- **Transport:** RabbitMQ (explicit topology — `BindExplicitly()` + `AutoProvision(false)`)
- **Saga store:** EF Core + PostgreSQL, registered via
  `.AddEntityFramework<OrderSagaDbContext>(p => { p.UseTransaction(); p.AddSagaCore(); })`
- **Host:** .NET Aspire 13.1.2 (PostgreSQL + RabbitMQ containers)
- `saga_states` schema: `id uuid, saga_name text, state json, created_at, updated_at, version uuid` (composite PK `id + saga_name`; `version` is the optimistic-concurrency token)

## Summary

A saga that **fans out N requests from `Initially`** and collects the N responses in a
single `During` state **loses updates**: after all responses are processed, only one of
the N result fields is persisted and the saga is stuck in its waiting state forever.

## Scenario

```
Initially
  .OnSend<StartOrderSagaCommand>()
  .StateFactory(...)                       // state.Id = OrderId
  .Send(GetStockInfoRequest)
  .Send(GetPriceInfoRequest)
  .Send(GetShippingInfoRequest)
  .Send(GetTaxInfoRequest)
  .Send(GetDiscountInfoRequest)
  .TransitionTo(AwaitingResponses);

During(AwaitingResponses)
  .OnSend<GetPriceInfoResult>()...TransitionTo(AwaitingResponses)     // self-loop
  .OnSend<GetShippingInfoResult>()...TransitionTo(AwaitingResponses)
  .OnSend<GetTaxInfoResult>()...TransitionTo(AwaitingResponses)
  .OnSend<GetDiscountInfoResult>()...TransitionTo(AwaitingResponses);
```

Each handler is a one-param `IEventRequestHandler<TRequest>` that does
`bus.SendAsync(new TResult(...))`; each `TResult : ICorrelatable` with
`CorrelationId => OrderId`. The five responses correlate to the **same** saga instance.

## Root cause

The EF saga store does **not serialize message processing per saga instance**. When
multiple correlated responses for the same instance arrive close together, they are
consumed **concurrently**, each in its own `DbContext`/transaction:
`SELECT` the saga row → mutate one field → `SaveChanges`. They all read the **same
`version`**, so their writes race and clobber each other.

Two distinct failure modes, both producing lost updates:

| Timing | What happens | Diagnostics |
|--------|--------------|-------------|
| Responses processed **before** the initial state row commits | All concurrent reads return empty → each performs an `INSERT` → only one wins on the PK | **Silent** — no exception, no retry |
| Responses processed **after** the row commits | Concurrent `UPDATE ... WHERE version=@old` → first bumps `version`, others affect 0 rows → `DbUpdateConcurrencyException` | Exception thrown but **not retried**; message is **acked anyway** |

In both cases the losing updates are discarded and the saga never reaches `Completed`.

## Deterministic reproduction

1. Fan out 5 requests from `Initially`; handle each response with a self-loop
   `.OnSend<TResult>()`.
2. Add a uniform `await Task.Delay(500, ct)` in each of the 5 handlers so all responses
   land together on the already-persisted row.
3. Single `POST /orders/saga`.

**Observed (EF command/update logging enabled):**

```
SELECTs = 9   INSERT = 1   UPDATE = 4   SaveChanges completed = 2
DbUpdateConcurrencyException = 3
```

```
Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
The database operation was expected to affect 1 row(s), but actually affected
0 row(s); data may have been modified or deleted since entities were loaded.
```

RabbitMQ `orderservice.saga.queue`: `publish == deliver == ack`, **`redeliver = 0`**
(no retry/redelivery). Final `saga_states` row: only **1 of 4** fields persisted; the
other three updates silently lost; `State` stuck at `AwaitingResponses`.

## A/B proof it is per-instance concurrency

Only change: add `.MaxConcurrency(1).MaxPrefetch(1)` to the saga queue so the saga
consumer processes one message at a time. Same load (including the 500 ms delays).

| | Concurrent (default) | Serialized (`MaxConcurrency 1` + `MaxPrefetch 1`) |
|---|---|---|
| `DbUpdateConcurrencyException` | 3 | **0** |
| `SaveChanges` completed | 2 | **5** |
| Fields persisted | 1 of 4 | **4 of 4** |

Serialized final state: `Price=29.97, ShippingCost=6.49, TaxAmount=5.69,
DiscountPercentage=10` — all persisted, zero conflicts.

## Requests to the architects

1. **Per-saga-instance serialization.** The EF saga store should serialize concurrent
   messages for the same instance (e.g. instance-level lock, `SELECT ... FOR UPDATE`, or
   single-flight dispatch) so fan-in patterns are safe by default rather than requiring
   a globally throttled consumer.
2. **Concurrency-conflict recovery.** On `DbUpdateConcurrencyException` the consume
   should reload-and-reapply (or nack → redeliver) instead of logging-and-acking.
   Today the update is silently lost.
3. **Pre-commit visibility window.** Responses processed before `Initially`'s state
   `INSERT` is visible cause concurrent consumers to each `INSERT`. This window needs
   defined handling.

## Notes / out of scope

- **Workaround (app-side):** `.MaxConcurrency(1).MaxPrefetch(1)` on the saga queue
  eliminates the conflicts, but throttles *all* sagas on that queue — a mitigation, not
  a fix.
- **Separate known issue (not concurrency-related):** the stock path in this repro still
  uses request/reply (`IEventRequestHandler<TRequest, TResult>` + `.OnReply<TResult>()`).
  Under the explicit topology the auto-reply lands on the transport's per-instance
  `response-*` queue and is dropped by the generic reply consumer, so `InStock` never
  persists. This is unrelated to the lost-update bug documented above.
