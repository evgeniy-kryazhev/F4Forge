#include "f4forge_runtime_abi.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>
#include <nethost.h>

#include <filesystem>
#include <mutex>
#include <string>

namespace {

using LoadAssemblyAndGetFunctionPointer = load_assembly_and_get_function_pointer_fn;
using ManagedInitialize = int (F4FORGE_CALL*)(void*);
using ManagedExecuteTask = void (F4FORGE_CALL*)(uint64_t);

struct State final {
    std::mutex mutex;
    HMODULE hostfxrModule{};
    hostfxr_handle hostContext{};
    ManagedInitialize initialize{};
    ManagedExecuteTask executeTask{};
    bool initialized = false;
};

State& GetState() noexcept
{
    static State state;
    return state;
}

std::wstring Utf8ToWide(F4ForgeStringView value)
{
    if (value.data == nullptr || value.length == 0) return {};
    const auto required = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data,
        static_cast<int>(value.length), nullptr, 0);
    if (required <= 0) return {};
    std::wstring result(static_cast<size_t>(required), L'\0');
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data,
            static_cast<int>(value.length), result.data(), required) != required)
        return {};
    return result;
}

bool LoadHostFxr(State& state) noexcept
{
    char_t hostfxrPath[4096]{};
    size_t hostfxrPathSize = sizeof(hostfxrPath) / sizeof(hostfxrPath[0]);
    get_hostfxr_parameters parameters{ sizeof(parameters), nullptr, nullptr };
    if (get_hostfxr_path(hostfxrPath, &hostfxrPathSize, &parameters) != 0) return false;
    state.hostfxrModule = LoadLibraryW(hostfxrPath);
    return state.hostfxrModule != nullptr;
}

template <typename T>
T GetHostFxrFunction(HMODULE module, const char* name) noexcept
{
    return reinterpret_cast<T>(GetProcAddress(module, name));
}

F4ForgeResult InitializeManaged(const F4ForgeRuntimeInitializeParams* params) noexcept
{
    if (params == nullptr || params->host == nullptr) return F4FORGE_RESULT_INVALID_ARGUMENT;
    auto& state = GetState();
    std::lock_guard lock(state.mutex);
    if (state.initialized) return F4FORGE_RESULT_SUCCESS;

    try {
        const auto root = Utf8ToWide(params->configDirectory);
        if (root.empty()) return F4FORGE_RESULT_INVALID_ARGUMENT;
        const auto assembly = std::filesystem::path(root) / L"F4Forge.DotNet.Runtime.dll";
        const auto runtimeConfig = std::filesystem::path(root) / L"F4Forge.DotNet.Runtime.runtimeconfig.json";
        if (!std::filesystem::exists(assembly) || !std::filesystem::exists(runtimeConfig))
            return F4FORGE_RESULT_RUNTIME_UNAVAILABLE;
        if (!LoadHostFxr(state)) return F4FORGE_RESULT_RUNTIME_UNAVAILABLE;

        const auto initializeForConfig = GetHostFxrFunction<hostfxr_initialize_for_runtime_config_fn>(
            state.hostfxrModule, "hostfxr_initialize_for_runtime_config");
        const auto getRuntimeDelegate = GetHostFxrFunction<hostfxr_get_runtime_delegate_fn>(
            state.hostfxrModule, "hostfxr_get_runtime_delegate");
        if (initializeForConfig == nullptr || getRuntimeDelegate == nullptr) return F4FORGE_RESULT_INTERNAL_ERROR;
        if (initializeForConfig(runtimeConfig.c_str(), nullptr, &state.hostContext) != 0)
            return F4FORGE_RESULT_RUNTIME_UNAVAILABLE;

        void* loadAssembly = nullptr;
        if (getRuntimeDelegate(state.hostContext, hdt_load_assembly_and_get_function_pointer, &loadAssembly) != 0 ||
            loadAssembly == nullptr)
            return F4FORGE_RESULT_INTERNAL_ERROR;
        const auto loadAssemblyFunction = reinterpret_cast<LoadAssemblyAndGetFunctionPointer>(loadAssembly);
        const wchar_t* typeName = L"F4Forge.DotNet.Runtime.Bootstrap, F4Forge.DotNet.Runtime";

        void* initialize = nullptr;
        if (loadAssemblyFunction(assembly.c_str(), typeName, L"Initialize", UNMANAGEDCALLERSONLY_METHOD,
                nullptr, &initialize) != 0 || initialize == nullptr)
            return F4FORGE_RESULT_INTERNAL_ERROR;
        void* executeTask = nullptr;
        if (loadAssemblyFunction(assembly.c_str(), typeName, L"ExecuteTask", UNMANAGEDCALLERSONLY_METHOD,
                nullptr, &executeTask) != 0 || executeTask == nullptr)
            return F4FORGE_RESULT_INTERNAL_ERROR;

        state.initialize = reinterpret_cast<ManagedInitialize>(initialize);
        state.executeTask = reinterpret_cast<ManagedExecuteTask>(executeTask);
        const auto result = state.initialize(const_cast<F4ForgeHostApi*>(params->host));
        if (result != static_cast<int>(F4FORGE_RESULT_SUCCESS)) return static_cast<F4ForgeResult>(result);
        state.initialized = true;
        return F4FORGE_RESULT_SUCCESS;
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult F4FORGE_CALL Initialize(const F4ForgeRuntimeInitializeParams* params) F4FORGE_NOEXCEPT
{
    return InitializeManaged(params);
}

void F4FORGE_CALL Shutdown(F4ForgeRuntimeHandle) F4FORGE_NOEXCEPT
{
    // CoreCLR and its unmanaged delegates remain loaded until process exit.
}

void F4FORGE_CALL ExecuteTask(const F4ForgeRuntimeTask* task) F4FORGE_NOEXCEPT
{
    try {
        auto& state = GetState();
        if (task == nullptr || state.executeTask == nullptr) return;
        state.executeTask(task->taskHandle);
    } catch (...) {
    }
}

const F4ForgeRuntimeInfo RuntimeInfo{
    F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
    sizeof(F4ForgeRuntimeInfo),
    { "dotnet", sizeof("dotnet") - 1 },
    { ".NET 10", sizeof(".NET 10") - 1 },
    1
};

const F4ForgeRuntimeProvider Provider{
    F4FORGE_RUNTIME_PROVIDER_ABI_VERSION,
    sizeof(F4ForgeRuntimeProvider),
    &RuntimeInfo,
    &Initialize,
    &Shutdown,
    &ExecuteTask
};

}

extern "C" __declspec(dllexport) const F4ForgeRuntimeProvider* F4FORGE_CALL F4ForgeDescribeRuntime() F4FORGE_NOEXCEPT
{
    return &Provider;
}
