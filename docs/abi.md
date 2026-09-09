# F4Forge ABI

## Contract

- Host ABI version: `2`.
- Runtime-provider ABI version: `1`.
- Target: Windows x64.
- Public structures use C linkage, fixed-width integer fields and `F4FORGE_CALL` (`__cdecl` with MSVC).
- Handles are opaque unsigned 64-bit values. Zero is invalid.
- String and byte views contain a pointer and byte length; strings are UTF-8 and need not be NUL terminated.

The current host table is 168 bytes. Its first 160 bytes are the required managed-host prefix. The final `waitModuleQuiescence` callback is available only when `structSize` is at least 168 and the callback pointer is non-null.

Consumers must validate the ABI version and required prefix before reading fields. Within one ABI version, compatible additions are append-only: existing field order, sizes, signatures and numeric result values remain stable. A producer may advertise a larger structure and a consumer must use `F4FORGE_HAS_FIELD` for optional tails.

## Acquisition

`F4ForgeGetHostApi` is a C export from `F4Forge.dll`:

```c
const F4ForgeHostApi* F4FORGE_CALL F4ForgeGetHostApi(void);
```

It returns a non-null, borrowed pointer to the process host table. Repeated calls return the same table address. The export does not initialize F4SE or runtime providers. Consumers must keep the DLL loaded and stop using the table before DLL teardown; no ownership or release operation is transferred.

Runtime providers expose the separate `F4ForgeDescribeRuntime` export and receive the host table through `F4ForgeRuntimeInitializeParams`.

## Layout And Ownership

The current layouts are:

| Structure | Size | Required current prefix |
|---|---:|---:|
| `F4ForgeHostApi` | 168 | 160 |
| `F4ForgeEndpointDefinition` | 64 | 64 |
| `F4ForgeRuntimeInfo` | 48 | 44 for current fields |
| `F4ForgeRuntimeInitializeParams` | 56 | 56 |
| `F4ForgeManagedBootstrapArgs` | 56 | 56 |
| `F4ForgeRuntimeProvider` | 40 | 40 |

Input views transfer no ownership. Endpoint names are copied when registered; callback and context pointers are retained according to the endpoint/module lifetime rules. Async request and payload bytes are copied before deferred execution. Provider metadata must remain valid for the provider's advertised lifetime.

## Results

`F4ForgeResult` is a stable 32-bit enum. `SUCCESS` is zero. Other values include invalid argument, invalid ABI version, invalid struct size, invalid/stale handle, inactive objects, wrong thread, unavailable runtime, timeout, not ready, cancellation and buffer-too-small results. Operation result retrieval has a retrieval status and a separate invocation result.

`OnUnload` is called only through an explicit runtime shutdown or reload boundary. Process termination is not a guaranteed managed unload notification; F4SE has no exit-process message.
