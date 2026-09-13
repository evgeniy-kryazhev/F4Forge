# Migrating from host ABI v2 to v3

F4Forge is pre-alpha and ABI v3 is intentionally breaking. There is no v2 compatibility shim.

## Version checks

- Require `F4FORGE_ABI_VERSION == 3` for the host table.
- Require `F4FORGE_RUNTIME_PROVIDER_ABI_VERSION == 2` for runtime providers.
- Continue validating `structSize` before reading any field.

## Runtime task scheduling

The v2 `queueTask(runtime, taskHandle, context)` callback accepted an unmanaged context pointer. In v3 it is:

```c
F4ForgeResult queueTask(F4ForgeRuntimeHandle runtime, uint64_t taskHandle);
```

Provider ABI v2 likewise changes `executeTask` from a pointer to `F4ForgeRuntimeTask` into two value arguments:

```c
void executeTask(F4ForgeRuntimeHandle runtime, uint64_t taskHandle);
```

Providers must keep their task state behind the numeric identifier, remove it after execution, and complete or cancel all pending work during shutdown. The host rejects stale, inactive, and quiescing runtime handles and revalidates the generation immediately before execution.

## Managed SDK

- Replace `context.Events.KeyDown` with `context.Input.KeyDown`.
- Event handlers now use `EventHandler` and `EventHandler<KeyDownEventArgs>`.
- Use `context.GameThread.InvokeAsync(action, cancellationToken)` for explicit game-thread dispatch.
- Endpoint and capability APIs are grouped under `context.Endpoints` and `context.Capabilities`; forwarding methods on the context remain available during the pre-alpha transition.
