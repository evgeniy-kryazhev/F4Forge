# Adding endpoint-based features

Use this guide for functionality that crosses the native/managed boundary. The Forms slice is the reference implementation:

- native contracts: [`native/core/forms/form_contracts.h`](../native/core/forms/form_contracts.h)
- native module: [`native/core/forms/form_module.h`](../native/core/forms/form_module.h) and [`form_module.cpp`](../native/core/forms/form_module.cpp)
- managed SDK: [`runtimes/dotnet/sdk/Forms`](../runtimes/dotnet/sdk/Forms)
- tests: [`native/tests/forms/FormModuleTests.cpp`](../native/tests/forms/FormModuleTests.cpp) and [`runtimes/dotnet/tests/FormServiceTests.cs`](../runtimes/dotnet/tests/FormServiceTests.cs)

## Endpoint or host ABI?

Add a versioned endpoint when the feature is game/domain functionality expressible as fixed bytes: queries, commands, events, or interception. Forms belongs here. Endpoints keep feature code in a vertical slice and do not grow the host function table.

Change the host ABI only when every runtime provider needs a new framework primitive that cannot be built from endpoints, such as endpoint registration/invocation, module lifetime, operation control, or runtime scheduling. An ABI change requires version/layout review, `structSize` compatibility handling, native and managed ABI declarations, and contract tests. Do not add a host callback merely because one feature needs native code.

## Vertical slice

Keep feature-specific code together instead of adding a central feature registry or factory:

```text
native/core/forms/
  form_contracts.h       endpoint names, versions, stable enums, binary DTOs
  form_module.h/.cpp     registration, lifecycle, handlers, CommonLib adapter
native/tests/forms/
  FormModuleTests.cpp
runtimes/dotnet/sdk/Forms/
  FormService.cs         endpoint resolution, encoding, public sync/async API
  FormType.cs            stable public SDK enum
  TESForm.cs             managed result model
runtimes/dotnet/tests/
  FormServiceTests.cs
```

Wire one module instance at the existing composition root (`BuiltinServices`) and expose one service from `F4ForgePluginContext`. That wiring is composition, not a feature registry.

## Names, versions, and contracts

Use a stable dotted name and resolve it with an explicit integer version. Forms registers and resolves these separate contracts:

| Endpoint | Version | Thread | Request | Response |
|---|---:|---|---:|---:|
| `game.forms.get_info` | `1` | `F4FORGE_THREAD_GAME_ONLY` | `FormRequest`, 4 bytes | `FormInfoResponse`, 12 bytes |
| `game.forms.get_name` | `1` | `F4FORGE_THREAD_GAME_ONLY` | `FormRequest`, 4 bytes | `FormNameResponse`, 260 bytes |

The version is endpoint metadata, not a suffix in the name. If a request, response, meaning, size, encoding, or thread contract must change incompatibly, register and consume version `2`; keep v1 unchanged while it is supported. Do not reinterpret or enlarge a v1 DTO in place.

Define only fixed-width, trivial native DTOs and assert every size:

```cpp
inline constexpr char GetInfoEndpoint[] = "game.forms.get_info";
inline constexpr uint32_t EndpointVersion = 1;

struct FormRequest final { uint32_t formId; };
struct FormInfoResponse final {
    uint32_t formId;
    uint32_t formType;
    uint32_t isValid;
};

static_assert(std::is_trivial_v<FormRequest> && sizeof(FormRequest) == 4);
static_assert(std::is_trivial_v<FormInfoResponse> && sizeof(FormInfoResponse) == 12);
```

The complete Forms name response is `uint32_t length` followed by `char utf8[256]`, for exactly 260 bytes. Strings are UTF-8 bytes, not native or managed string objects, and are not required to be NUL terminated. Lengths are byte counts. Numeric fields use the Windows x64 little-endian wire representation; managed code must encode explicitly:

```csharp
Span<byte> request = stackalloc byte[4];
BinaryPrimitives.WriteUInt32LittleEndian(request, formId);

Span<byte> response = stackalloc byte[12];
var result = _endpoints.Invoke(endpoint, request, response, out var size);
if (result != F4ForgeResult.Success) return result;
if (size != response.Length) return F4ForgeResult.InternalError;

var type = (FormType)BinaryPrimitives.ReadUInt32LittleEndian(response[4..]);
```

Never expose a CommonLib enum as a public contract. Map it in the native adapter to a deliberately small, stable enum, as `StableType(RE::ENUM_FORM_ID)` maps to `forms::FormType`. Give the C# enum the same explicit `uint` values. Unknown future CommonLib values must map to a defined fallback such as `Other`, not leak through numerically.

## Implementation template

1. Define endpoint names, one shared version constant, fixed DTOs, capacities, stable enums, and size assertions in the slice's contracts header.
2. Add a narrow injectable domain seam. Forms uses `FormLookup`; production `GameFormLookup` contains all `RE::TESForm`/CommonLib access, while tests inject `FakeLookup`.
3. Register each endpoint from the module with exact kind, version, sizes, and thread policy. Roll back all registrations if a multi-endpoint slice starts only partially.
4. Validate pointers and sizes at the C callback boundary, zero response DTOs before work, and set `responseSize` consistently. The registry also enforces declared request size, response capacity, and thread policy.
5. Catch every exception before it can cross a `noexcept` C ABI callback and return `F4FORGE_RESULT_INTERNAL_ERROR`. Translate expected states to specific results such as `NOT_READY`, `NOT_FOUND`, and `BUFFER_TOO_SMALL`.
6. Resolve endpoints lazily in the managed service with the same explicit version. Missing handles return `CapabilityUnavailable`; validate the exact returned size and every embedded length before decoding.
7. Expose the service through `F4ForgePluginContext`, preserving result codes rather than replacing normal failures with exceptions.

Minimal native registration follows Forms:

```cpp
const F4ForgeEndpointDefinition definition{
    sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_METHOD,
    forms::EndpointVersion, F4FORGE_ENDPOINT_NONE, F4FORGE_THREAD_GAME_ONLY,
    sizeof(forms::FormRequest), sizeof(forms::FormInfoResponse), 0,
    { forms::GetInfoEndpoint, sizeof(forms::GetInfoEndpoint) - 1 },
    &GetInfoThunk, this
};
return _endpoints.Register(definition, &_owner, &_getInfoEndpoint) ==
    F4FORGE_RESULT_SUCCESS;
```

Minimal managed resolution should name the version:

```csharp
private const uint EndpointVersion = 1;
_getInfo = _endpoints.Resolve("game.forms.get_info", EndpointVersion);
```

## Threads, lifecycle, and readiness

Choose the narrowest truthful `F4ForgeThreadPolicy`. Forms touches game state and therefore uses `GAME_ONLY`. Its synchronous `Get` never marshals: a worker-thread call returns `WrongThread`. Provide an explicitly named asynchronous SDK method for callers that need marshalling:

```csharp
public Task<FormQueryResult> GetAsync(
    uint formId, CancellationToken cancellationToken = default) =>
    _gameThread.InvokeAsync(() => {
        var result = Get(formId, out var form);
        return new FormQueryResult(result, form);
    }, cancellationToken);
```

Do not hide scheduling or blocking inside a synchronous method. For raw endpoint asynchronous operations use `EndpointService.InvokeAsync`/`EmitAsync`; for a managed game-thread API shaped like Forms, use `GameThreadDispatcher` and propagate the plugin cancellation token.

Separate registration from data readiness. Forms may register during `Start`, but returns `NOT_READY` until `OnGameDataReady`. `Stop` clears readiness before started state; destruction stops the module and invalidates every endpoint owned by its `EndpointOwner`. Startup must be idempotent, failed partial registration must be rolled back, and no handler may access game data before the lifecycle signal that makes it valid.

Dependency injection stops immediately outside platform code. The module depends on `FormLookup`, tests supply a fake, and only `GameFormLookup` calls CommonLib. On the managed side, `FormService` depends on `EndpointService` and `GameThreadDispatcher`; bridge and scheduler fakes test encoding and dispatch without loading native code.

## Test checklist

Native:

- contract sizes and triviality compile-time assertions
- successful registration and invocation for every endpoint
- not-ready before the lifecycle signal and success after it
- exact request size, response capacity, null argument, and wrong-thread rejection
- not-found and other domain result mappings
- fixed-capacity boundaries, including exactly full and one byte too large
- stable enum mapping and UTF-8 byte length/content
- injected dependency throwing, translated to `INTERNAL_ERROR`
- partial-start rollback, stop/invalidation, and idempotent lifecycle where applicable

Managed:

- exact endpoint names and version resolution
- request bytes and response decoding for every DTO field
- exact response-size and embedded-length validation
- missing endpoint returns `CapabilityUnavailable`
- native failure results pass through and leave no partial public object
- explicit async method uses the scheduler and honors cancellation
- public model and stable enum values remain independent of CommonLib
- service is available from `F4ForgePluginContext`

Run `./scripts/test.ps1` and `./scripts/cppcheck.ps1`. Add a native test target to `native/xmake.lua` and to both native target lists in `scripts/test.ps1`; add managed tests to the existing test project so packaging/build validation sees the new SDK files.

## Package and documentation checklist

- Ensure new native sources are included in the relevant xmake targets.
- Ensure SDK `.cs` files are included by the SDK project and therefore in `F4Forge.DotNet.Sdk.dll`.
- Keep framework/demo staging and ZIP contents correct if a new assembly or runtime asset is introduced.
- Document public SDK usage in `README.md` when users need discovery-level guidance.
- Update [`docs/abi.md`](abi.md) only for ABI-level layouts, ownership, scheduling, or endpoint wire contracts; do not duplicate the implementation guide there.
- Add migration notes when replacing a shipped endpoint version, and state how long the old version remains available.
- Verify all relative Markdown links and inspect `git diff` before committing.

## Anti-patterns

- Native pointers, CommonLib objects, `std::string`, managed objects, or delegates in endpoint DTOs.
- Feature-specific callbacks added to `F4ForgeHostApi` instead of feature endpoints.
- A central switch, registry, or factory that knows every feature and routes feature payloads.
- Mutating a published endpoint contract instead of adding a new endpoint version.
- Implicit game-thread marshalling or waiting in a synchronous SDK method.
- Blocking event/endpoint handlers or waiting synchronously on async work.
- Holding locks while invoking native thunks, managed delegates, subscribers, interceptors, or injected dependencies.
- Catching exceptions only in managed code or allowing a C++ exception to cross the C ABI boundary.
- Reading game state merely because endpoints are registered; readiness is a separate lifecycle state.
