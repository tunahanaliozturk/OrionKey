# Snowflake worker IDs

OrionKey's `Snowflake` strategy generates 64-bit, time-ordered `long` ids. Every Snowflake
id is composed of a timestamp, a per-process **worker ID**, and a per-millisecond sequence
counter. This page explains the worker ID, how OrionKey resolves it, and how to pin it
correctly in multi-instance deployments.

## What a worker ID is

The worker ID occupies **10 bits** of the Snowflake layout, so its valid range is
**0 to 1023**. It exists to keep ids unique across processes: within a single millisecond,
two processes can each mint ids, and only the worker ID distinguishes their output.

This makes the worker ID safety-critical. If two running instances share the same worker
ID, they can produce **colliding ids** for any millisecond in which both generate ids at
the same sequence position. A worker ID must therefore be unique per running instance, not
merely per machine or per deployment.

## How OrionKey resolves the worker ID

OrionKey resolves the worker ID once, at first use, from three sources in priority order:

1. **Explicit configuration.** A value set through `OrionKey.Configure` always wins:

   ```csharp
   OrionKey.Configure(o => o.SnowflakeWorkerId = 7);
   ```

2. **The `ORIONKEY_WORKER_ID` environment variable.** If no explicit value was set,
   OrionKey reads this variable and parses it as an integer in the range 0-1023.

3. **A machine-name hash fallback.** If neither of the above is provided, OrionKey derives
   a worker ID by hashing the machine name into the 0-1023 range.

## The auto-derive warning

When OrionKey falls back to the machine-name hash, it writes a **one-time warning** noting
that the worker ID was auto-derived. The machine-name hash is convenient for a single
instance and for local development, but it is not collision-free: two machines can hash to
the same value, and a single machine running multiple instances (containers, app-pool
recycling, scaled processes) gives every instance the *same* hash.

In any multi-instance deployment you should treat the warning as a prompt to **pin the
worker ID explicitly** through `OrionKey.Configure` or `ORIONKEY_WORKER_ID`, assigning each
instance a distinct value.

## Kubernetes example

A `StatefulSet` gives each pod a stable ordinal name (`app-0`, `app-1`, `app-2`, ...). The
ordinal suffix is a natural, unique worker ID. Map the pod name into
`ORIONKEY_WORKER_ID` and let the container derive the ordinal at startup:

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: orders
spec:
  serviceName: orders
  replicas: 4
  template:
    spec:
      containers:
        - name: orders
          image: example/orders:1.0.0
          env:
            - name: POD_NAME
              valueFrom:
                fieldRef:
                  fieldPath: metadata.name
            # Map the pod ordinal (the digits after the last '-') to the worker id.
            - name: ORIONKEY_WORKER_ID
              value: "$(POD_NAME##*-)"
```

If the substitution above is not expanded by your tooling, derive the ordinal in the
container entrypoint instead:

```sh
export ORIONKEY_WORKER_ID="${POD_NAME##*-}"
```

Each pod (`orders-0` through `orders-3`) then runs with a unique worker ID in the range
0-3, and the ids minted across the StatefulSet cannot collide.
